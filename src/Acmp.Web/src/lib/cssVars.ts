/*
 * WBS-40.5 / DW-022 (DEC-174): the SPA's JSX carries no `style` prop. A static value is a CSS class; a
 * value only known at render time (a percentage, a graph coordinate, a tone picked from data) reaches
 * the stylesheet as a CSS custom property, and the stylesheet decides which property it feeds:
 *
 *   <span className="rpt-bar-fill" ref={cssVars({ '--pct': `${b.pct}%` })} />
 *   .rpt-bar-fill { inline-size: var(--pct); }
 *
 * A callback-ref factory rather than a hook, so it works inside `.map()`. A name that a later render
 * stops passing — or passes as null/undefined — is removed, so a variable never outlives the value it
 * carried. Hygiene only, not a CSP measure: `style-src 'self'` already allowed the style prop, because
 * react-dom writes it through the CSSOM exactly as this does (DW-022's own row).
 */
export type CssVars = Readonly<Record<`--${string}`, string | null | undefined>>;

const applied = new WeakMap<HTMLElement | SVGElement, ReadonlySet<string>>();

export function setCssVars(el: HTMLElement | SVGElement, vars: CssVars): void {
  const next = new Set<string>();
  for (const [name, value] of Object.entries(vars)) {
    if (value == null) continue;
    el.style.setProperty(name, value);
    next.add(name);
  }
  for (const name of applied.get(el) ?? []) {
    if (!next.has(name)) el.style.removeProperty(name);
  }
  applied.set(el, next);
}

export function cssVars(vars: CssVars): (el: HTMLElement | SVGElement | null) => void {
  return (el) => {
    if (el) setCssVars(el, vars);
  };
}
