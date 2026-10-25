# Inspector

[Documentation index](README.md)

When you select a `GalaxyShader/Toon` material, the dedicated GalaxyToon Inspector is shown.

---

## Layout

From top to bottom:

1. Display language and logo
2. Shader ON/OFF button
3. Render Mode, Render Face, Render Queue Offset
4. Presets
5. Compare Textures
6. Search, expand/collapse, and reset buttons
7. One section per feature (collapsible)
8. Render Queue
9. Display Settings

---

## Display Language

Switch between **日本語 / English** with the dropdown at the top left.

- The choice is saved per PC (EditorPrefs).
- Changing the language does not change which sections are open or any material values.

## Shader ON/OFF

Pressing **Shader ON — click to turn OFF (compare with URP Lit)** draws **every material** that uses GalaxyToon with the standard URP Lit, using the same textures. Use it to compare against the toon look.

- Material values are not changed.
- Outlines are hidden while it is OFF.
- The shortcut is **Ctrl+Shift+T** (menu **Tools > GalaxyToon > シェーダー OFF** (Shader OFF)).

## Render Mode

| Render Mode | Use for | Behavior |
|---|---|---|
| Opaque | Normal surfaces | Writes depth, casts shadows, draws outlines |
| Cutout | Hair tips, lace, mesh | Skips pixels whose texture alpha is below **Alpha Cutoff** |
| Transparent | Glass, blush, tears | Blends with what is behind. Outlines, shadow casting, and depth writing are turned off |

Choosing a render mode automatically sets blending, ZWrite, the render queue, and which passes are used.

## Render Face

| Option | Use for |
|---|---|
| Both | Hair, skirts, thin cloth |
| Front | Normal models (cheapest) |
| Back | Special uses, such as spheres seen from inside |

## Render Queue Offset

Set text, decals, and stickers layered on the same surface to **+1**. They are drawn after the surface below and no longer disappear into it. The range is -5 to +5.

---

## Presets

Press a button named after a body part or material to fill in matching values at once. **Textures, Main Color, and Render Mode are not changed** (except presets that require transparency, such as Tears / Drops, which also change the render mode).

| Row | Contents |
|---|---|
| Character | Basic settings per body part (skin, face, hair, ...). Sets shadow, rim, outline, and other values |
| Environment | For buildings and props. Receives scene lights, ambient light, and shadows. No outline |
| Skin / Cloth Surface | Overwrites only the surface look: sweat, wet, fabric, latex, leather, ... |
| Add (on top of current) | Adds only sweat, liquid, dirt, or effects without changing other values. Can be stacked |
| Character Finish | Adds polish such as hair shadow, eyes, glitter, and fade when close |

**Reset All but Basic** keeps textures, Main Color, normal maps, Render Mode, and Render Face, and resets everything else to defaults.

See [Presets](presets.md) for the contents of each preset.

---

## Compare Textures

Press **Off** next to a texture to remove it temporarily without reassigning it. Press **On** to restore it.

- The texture reference stays in the material while it is off.
- **All Off** / **All On** toggle every texture at once.

## Search

Type in the search field to show only matching settings, grouped by section.

- Separate words with spaces to show only settings that contain all of them.
- You can search by setting name (Japanese or English), internal name (such as `_ShadowColor`), or section name.
- Case is ignored.

## Expand, Collapse, and Reset

| Button | Behavior |
|---|---|
| One at a Time | When ON, opening a section closes the others (accordion) |
| Expand / Collapse | Opens / closes all sections |
| Reset All | Resets every setting to the shader defaults, including textures and render mode (asks first) |
| Reset Section | At the top right of each section. Resets only that section (asks first) |

Which sections are open is remembered.

## In-Use Indicators

- Sections whose feature is active show **● In use** and get a colored background.
- Settings changed from their defaults get a colored field.

Change the colors in **Display Settings** at the bottom. **Reset Colors** restores the defaults.

---

## Automatic Feature ON/OFF

GalaxyToon strips unused features from the shader so they are neither computed nor built. The Inspector switches them ON/OFF automatically.

| A feature turns ON when you | Examples |
|---|---|
| Assign a texture | Normal map, shadow ramp, layers, metallic / smoothness map |
| Set the color alpha (strength) above 0 | Highlight, rim, anisotropic, backlight, glitter, Shadow 2 / Shadow 3 |
| Set the value above 0 | Flatten Face Shading, metallic / smoothness, additional lights, Receive Hair Shadow |
| Turn the toggle ON | Wet / sweat / liquid, dissolve, depth rim, SDF face shadow, eyes, triplanar, anime shading |

Setting the outline width to 0 stops drawing the outline entirely.

Keywords are not switched automatically when you change material values from a script. Run **全マテリアルの機能を整理** (Clean Up All Materials) from [Editor Tools](editor-tools.md) to fix the keywords of all materials to match their values.
