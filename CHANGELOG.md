# Changelog

## 0.1.0 — first cut

- A Larder button on the inventory screen opens a side panel with the best three foods for your
  goal (Balanced, Health, Stamina, Eitr) from your bag, chests and carts you can open within 20 m,
  and placed feasts.
- Each planned food shows where it is and what to do: eat now, can refresh now, active with time
  left, or eat in Xm when your slots are full, and tells you to refresh a planned food first
  when eating would push it out. Foods outside the plan show when their slot frees.
- Eat button for planned foods in your bag, using the game's own right-click path.
- Cook next: the one dish that would improve the combo and that you can make now, plus an
  "if you had…" pick, each with its ingredients as have / need and intermediate steps.
- Undiscovered recipes are never suggested unless you turn on ShowUndiscovered.
- Everything read from the game at runtime, so new foods and other mods' foods are included.
- `larder`, `larder foods`, `larder goal <name>` console commands.
