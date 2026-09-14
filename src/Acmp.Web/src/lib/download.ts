/**
 * AC-164 / AC-165: start a browser download from a link the server signed with `Content-Disposition: attachment`.
 * A plain <a> with no target keeps the app on its page (the response is a download, not a navigation) and needs no
 * popup permission - unlike `window.open` after an `await`, which a popup blocker can silently swallow (DEF-172).
 * The file name comes from the signed disposition, so it works cross-origin, where an `<a download>` attribute is
 * ignored (DEF-174). The attribute is still set: where the store is served over the app's origin (the nginx
 * proxy), a response the store REFUSES (an expired link, a file deleted a moment ago) is then saved as a file
 * instead of replacing the app with the store's XML error page. Cross-origin, that refusal still navigates.
 */
export function startDownload(url: string): void {
  // Only a web link may be followed - never javascript: or data: (the player's safeHttps guard, for the same
  // reason). http is allowed because the on-prem and E2E stacks serve the object store over the app's origin.
  const protocol = new URL(url, window.location.href).protocol;
  if (protocol !== 'https:' && protocol !== 'http:') throw new Error(`refusing to download from a ${protocol} link`);
  const a = document.createElement('a');
  a.href = url;
  a.download = ''; // the signed Content-Disposition names the file; this only keeps a same-origin refusal off the page
  a.rel = 'noopener';
  document.body.appendChild(a);
  a.click();
  a.remove();
}
