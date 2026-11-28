# U10 — UI spacing and localization audit

## Scope

Page-by-page spacing/token review for Home, Protection, Storage, Apps, Toolbox, Debloat, Memory,
Settings and the floating toolbar. The linter is the ratchet for XAML spacing and colour literals.

## 2026-11-28 baseline

| Area | Before U10 | After U10 | Result |
|---|---:|---:|---|
| Settings | 31 literals | 24 literals | 7 fewer |
| Main window / views | baseline ratchet unchanged | unchanged except Settings | 360 total |
| Lint total | 367 literals | 360 literals | baseline only decreases |
| Localization | no `.resx` foundation | English and Urdu `.resx` + culture/RTL startup hook | foundation added |

## Token rules

- Uniform spacing uses `Thickness.1` through `Thickness.10`.
- Page margins use `Page.MarginCompact`, `Page.Margin`, or `Page.MarginExpanded`.
- Card padding uses `Card.Padding`, `Card.PaddingExpanded`, or `Card.PaddingNarrow`.
- Row/button spacing uses `Row.*` and `Button.*`.
- Radii use `Radius.*`.
- Colours remain in the Light/Dark theme dictionaries.

## Responsive and localization status

- 360 / 560 / 1280 layout rules exist in `MainWindow` breakpoints and existing page work.
- Live 360 / 560 / 1280 captures remain part of U10/U11 verification, not a claim from the 360
  lint pass alone.
- English is the default; Urdu culture and RTL direction are applied from persisted settings at
  startup. The resource foundation currently defines the primary navigation keys.
- Runtime switching of every XAML string is not yet complete; U10 intentionally stops at the
  `.resx` foundation and applies Urdu culture/RTL without claiming a complete translation.
