// Bans Date methods that produce ambiguous locale-dependent formats (d/m/y vs m/d/y).
// Use useFormatDate() from @repo/ui/hooks/useSmartDate instead, which formats dates
// consistently according to the app's selected language.

const BANNED_METHODS = ["toLocaleDateString", "toLocaleString", "toLocaleTimeString"];

const rule = {
  meta: {
    type: "problem",
    docs: {
      description:
        "Disallow ambiguous date formatting methods. Use useFormatDate() from @repo/ui/hooks/useSmartDate instead.",
    },
  },
  create(context) {
    return {
      CallExpression(node) {
        if (
          node.callee.type !== "MemberExpression" ||
          node.callee.property.type !== "Identifier" ||
          !BANNED_METHODS.includes(node.callee.property.name)
        ) {
          return;
        }

        context.report({
          node,
          message: `\`${node.callee.property.name}()\` produces ambiguous date formats. Use useFormatDate() from @repo/ui/hooks/useSmartDate instead.`,
        });
      },
    };
  },
};

const plugin = {
  meta: {
    name: "date-formatting",
  },
  rules: {
    "no-ambiguous-date-formatting": rule,
  },
};

export default plugin;
