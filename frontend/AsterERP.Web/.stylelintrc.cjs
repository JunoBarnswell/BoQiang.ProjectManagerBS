module.exports = {
  "extends": [
    "stylelint-config-standard"
  ],
  "plugins": [
    "stylelint-order"
  ],
  "rules": {
    "at-rule-no-unknown": [true, {
      "ignoreAtRules": [
        "theme"
      ]
    }],
    "import-notation": "string",
    "selector-class-pattern": null,
    "alpha-value-notation": null,
    "color-function-notation": null,
    "color-hex-length": null,
    "custom-property-empty-line-before": null,
    "declaration-block-single-line-max-declarations": null,
    "font-family-no-missing-generic-family-keyword": null,
    "media-feature-range-notation": null,
    "no-descending-specificity": null,
    "rule-empty-line-before": null,
    "no-empty-source": null
  }
};
