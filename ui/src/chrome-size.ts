// Sticky page headers and action bars sit just below the top bar and above the bottom tabs. Both heights depend
// on the screen (the top bar's breadcrumb wraps on a phone), so they are measured instead of hard-coded.
const tracked = [
  { selector: ".topbar", variable: "--topbar-h" },
  { selector: ".bottom-tabs", variable: "--bottom-tabs-h" },
];

export function trackChromeSize() {
  const root = document.documentElement;
  const current = new Map<string, Element | null>();
  const observer = new ResizeObserver((entries) => {
    for (const entry of entries) {
      const item = tracked.find((t) => current.get(t.selector) === entry.target);
      if (item)
        root.style.setProperty(
          item.variable,
          `${Math.round(entry.target.getBoundingClientRect().height)}px`,
        );
    }
  });
  const attach = () => {
    for (const { selector, variable } of tracked) {
      const element = document.querySelector(selector);
      if (element === (current.get(selector) ?? null)) continue;
      const previous = current.get(selector);
      if (previous) observer.unobserve(previous);
      current.set(selector, element);
      if (element) observer.observe(element);
      else root.style.removeProperty(variable);
    }
  };
  new MutationObserver(attach).observe(document.body, {
    childList: true,
    subtree: true,
  });
  attach();
}
