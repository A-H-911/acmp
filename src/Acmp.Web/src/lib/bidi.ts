/** AC-168: the text-level <bdi> - a name interpolated INTO a translated sentence, where no element can wrap it, is
 *  held between FIRST STRONG ISOLATE and POP DIRECTIONAL ISOLATE, so a Latin name ("E2E Secretary") does not
 *  reorder against the Arabic words, digits or punctuation around it. */
export const isolate = (s: string): string => `⁨${s}⁩`;
