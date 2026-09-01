#!/usr/bin/env sh
# The one runnable check behind check-container-health.sh (WBS-26.4 / SC-042).
#
#   sh deploy/scripts/check-container-health.test.sh
#
# A control in the "detects but does not TELL" family has to be proven by FORCING each refusal, not
# by watching it pass on a healthy stack: a script that always returns 0 is indistinguishable from a
# working one right up until the night it matters. `docker` is stubbed on PATH so every branch is
# reachable without a container runtime — which is also what lets this run in CI's compose job,
# exactly as assert-oneshot.test.sh does.
#
# CASE 3 IS THE ONE THIS PROJECT KEEPS RE-LEARNING (LL-013, trap 31, DEF-078): a check that inspects
# nothing must FAIL, because a pass over an empty set reads identically to a pass over a healthy one.
set -eu

here="$(cd "$(dirname "$0")" && pwd)"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

# The stub answers three questions the script asks, keyed on how it is called:
#   docker compose ... ps --services   -> FAKE_SERVICES
#   docker compose ... ps -q <svc>     -> a cid, unless the service is in FAKE_ABSENT
#   docker inspect -f <tmpl> <cid>     -> that service's health, from FAKE_HEALTH ("svc=state ...")
cat > "$tmp/docker" <<'STUB'
#!/usr/bin/env sh
last=""; prev=""
for a in "$@"; do prev="$last"; last="$a"; done
case "${1:-}" in
  compose)
    case " $* " in
      *" ps --services "*) printf '%s\n' ${FAKE_SERVICES:-} ;;
      *" ps -q "*)
        svc="$last"
        case " ${FAKE_ABSENT:-} " in *" $svc "*) : ;; *) printf 'cid-%s\n' "$svc" ;; esac ;;
    esac ;;
  inspect)
    svc="${last#cid-}"
    for pair in ${FAKE_HEALTH:-}; do
      case "$pair" in "$svc="*) printf '%s\n' "${pair#*=}"; exit 0 ;; esac
    done
    printf 'none\n' ;;
esac
STUB
chmod +x "$tmp/docker"

# `aws` is stubbed too: cases 5 and 6 assert the alert is attempted, and an unstubbed aws would
# either be absent (masking the branch) or real (publishing from a test run).
#
# ⚠ THE MARKER GOES TO A FILE, NOT TO stdout, AND THAT IS THE POINT. The script pipes the publish to
# `>/dev/null 2>&1` — correctly, so AWS chatter never lands in a cron log — which means a stdout
# marker proves nothing about whether aws ran. The first version of this test asserted on stdout and
# FAILED for exactly that reason; the fix was to the instrument, not to the assertion. LL-009's
# family: a probe that cannot see the thing it is aimed at reads identically to a thing not happening.
cat > "$tmp/aws" <<'STUB'
#!/usr/bin/env sh
[ -z "${FAKE_AWS_MARKER:-}" ] || printf 'published\n' >> "$FAKE_AWS_MARKER"
exit "${FAKE_AWS_RC:-0}"
STUB
chmod +x "$tmp/aws"

# `curl` is stubbed for the Seq half. Same file-marker discipline as aws and for the same reason: the
# script pipes the POST to >/dev/null 2>&1, so nothing it writes to stdout can prove it ran. The marker
# also CAPTURES the body, which is what lets case 9 assert the alert has a stable key to fire on.
cat > "$tmp/curl" <<'STUB'
#!/usr/bin/env sh
if [ -n "${FAKE_CURL_MARKER:-}" ]; then cat >> "$FAKE_CURL_MARKER"; fi
exit "${FAKE_CURL_RC:-0}"
STUB
chmod +x "$tmp/curl"

PATH="$tmp:$PATH"
export PATH

fails=0

# Checks BOTH halves at once — the verdict (exit code) and the telling (what reached stdout/stderr).
# Asserting only the exit code would rebuild the defect one level up, which is DEF-054's whole story.
check() {  # <name> <expect-pass|expect-fail> <substring-that-must-appear|->
  name="$1"; want="$2"; must="$3"
  set +e
  out="$(sh "$here/check-container-health.sh" -f /dev/null 2>&1)"
  code=$?
  set -e

  if [ "$want" = "expect-pass" ] && [ "$code" -ne 0 ]; then
    echo "FAIL  $name: expected exit 0, got $code"; echo "$out"; fails=$(( fails + 1 )); return
  fi
  if [ "$want" = "expect-fail" ] && [ "$code" -eq 0 ]; then
    echo "FAIL  $name: expected a non-zero exit, got 0 — the check did not refuse"; echo "$out"
    fails=$(( fails + 1 )); return
  fi
  if [ "$must" != "-" ]; then
    case "$out" in
      *"$must"*) : ;;
      *) echo "FAIL  $name: output never mentioned '$must'"; echo "$out"; fails=$(( fails + 1 )); return ;;
    esac
  fi
  echo "OK    $name (exit $code)"
}

# 1 — the healthy stack. The only case that may pass, and it still reports its subject COUNT, so a
#     clean run says how much it looked at rather than merely that it looked.
FAKE_SERVICES="api web worker" FAKE_HEALTH="api=healthy web=healthy worker=healthy" \
  check "all healthy passes and reports the count" expect-pass "3 container(s)"

# 2 — THE DEFECT ITSELF: the steady state nothing observed. api is unhealthy, no dependent has
#     restarted (the interlock is deliberately gone, DEF-079), and until this script nothing noticed.
FAKE_SERVICES="api web worker" FAKE_HEALTH="api=unhealthy web=healthy worker=healthy" \
  check "an unhealthy container refuses and NAMES it" expect-fail "api(unhealthy)"

# 3 — nothing running declares a healthcheck. A pass here would be a green control that never looked.
FAKE_SERVICES="api web" FAKE_HEALTH="" \
  check "zero inspectable containers refuses" expect-fail "inspected 0 running containers"

# 4 — compose named no services at all: wrong arguments, or the stack is down. Also never a pass.
FAKE_SERVICES="" \
  check "no services at all refuses" expect-fail "named no services"

# 5 — the TELLING half, on the cloud path. Unhealthy WITH a topic set must actually INVOKE aws; the
#     marker file proves the branch was reached, where the log line alone would only prove the script
#     believes it published.
marker="$tmp/published"
FAKE_AWS_MARKER="$marker" ACMP_ALERT_TOPIC_ARN=arn:aws:sns:us-east-1:1:acmp \
  FAKE_SERVICES="api" FAKE_HEALTH="api=unhealthy" \
  check "with a topic set, the alert is published" expect-fail "alert published to"
if [ -s "$marker" ]; then
  echo "OK    aws sns publish was actually invoked (marker file written)"
else
  echo "FAIL  aws sns publish was NEVER invoked — the log line said otherwise"; fails=$(( fails + 1 ))
fi

# 6 — THE PUBLISH ITSELF FAILS. This is the branch that decides whether the control can tell when it
#     cannot tell. A silent fall-through here would leave the finding unreported while the log looks
#     like a normal alerting run — DEF-023/051/054's shape, one level deeper.
FAKE_AWS_MARKER="$tmp/published2" FAKE_AWS_RC=1 ACMP_ALERT_TOPIC_ARN=arn:aws:sns:us-east-1:1:acmp \
  FAKE_SERVICES="api" FAKE_HEALTH="api=unhealthy" \
  check "a refused publish says the finding is UNREPORTED" expect-fail "ALERT PUBLISH FAILED"

# 7 — the TELLING half on-prem, where there is no SNS topic. It must say so LOUDLY rather than fall
#     through silently: a control that cannot tell, and does not admit it, is DEF-023/051/054.
#     ⚠ The expected string is "NO SNS ALERT SENT", not "NO ALERT SENT". When the Seq half was added the
#     log line was qualified — Seq is still notified on-prem, only the SNS push is absent — and this case
#     FAILED on the rename, which is the test doing its job on its own author.
FAKE_SERVICES="api" FAKE_HEALTH="api=unhealthy" \
  check "with no SNS topic, it says so and still emits to Seq" expect-fail "NO SNS ALERT SENT"

# 8 — a container in `starting` is not healthy. Docker reports `starting` during start_period, and
#     treating it as a pass would make the check blind exactly while a stack is coming back up.
FAKE_SERVICES="api" FAKE_HEALTH="api=starting" \
  check "a starting container is not counted as healthy" expect-fail "api(starting)"

# 9 — THE SEQ HALF IS THE OPERATOR'S CHOSEN NOTIFICATION PATH AND IT IS THE ONE THAT EXISTS IN EVERY
#     ENVIRONMENT: on-prem has no SNS topic at all. So it must be shown to actually POST, not merely to
#     log that it did. The marker file proves curl ran; the log line alone would prove only that the
#     script believes it ran (DEF-124's distinction, one layer out).
seqmark="$tmp/seq"
FAKE_CURL_MARKER="$seqmark" FAKE_SERVICES="api" FAKE_HEALTH="api=unhealthy" \
  check "the Seq event is emitted" expect-fail "emitted to Seq"
if [ -s "$seqmark" ]; then
  echo "OK    curl POSTed to Seq (marker file written)"
  # The ALERT KEYS ON THE EVENT TYPE, not on a message substring, so that a Seq rule survives someone
  # rewording this script's prose. Assert the key is actually in the body that went over the wire.
  case "$(cat "$seqmark")" in
    *'"EventType":"Deploy.ContainerUnhealthy"'*)
      echo "OK    the CLEF body carries EventType Deploy.ContainerUnhealthy for a Seq rule to key on" ;;
    *) echo "FAIL  the CLEF body has no EventType key — a Seq alert would have to match on prose"
       fails=$(( fails + 1 )) ;;
  esac
else
  echo "FAIL  curl was NEVER invoked — the log line said the event was emitted"; fails=$(( fails + 1 ))
fi

# 10 — SEQ IS DOWN. The notifier that cannot notify must SAY so; a silent fall-through would leave the
#      operator's alert permanently un-fired while the log reads like a normal alerting run.
FAKE_CURL_RC=1 FAKE_SERVICES="api" FAKE_HEALTH="api=unhealthy" \
  check "a refused Seq post says no alert can fire" expect-fail "SEQ EMIT FAILED"

[ "$fails" -eq 0 ] || { echo "check-container-health.test: $fails case(s) failed"; exit 1; }
echo "check-container-health.test: all 10 cases behaved as specified."
