# BossFight2D ArtPack

Pack artistico autocontenido para la vertical slice del jefe topo mecanico.
Los archivos nuevos viven aqui y no sustituyen los recursos existentes del proyecto.

## Contenido

- `Concepts/Characters/character_roster_3d_three_quarter.png`: roster de cinco personajes jugables en render 3D y vista de tres cuartos. Sirve como guia de color, silueta, retratos y futuras versiones 2D.
- `Concepts/Boss/iron_burrower_boss_3d_three_quarter.png`: concepto del jefe Iron Burrower, con energia cian/naranja y acentos rojos para la fase dos.
- `Concepts/Environment/arena_industrial_top_down_concept.png`: referencia de arena industrial vista desde arriba para suelo, paredes, luces y decoracion.
- `UI/Emotes/emotes_12_reactions.png`: doce reacciones para HUD, menus y estados de combate.
- `UI/Icons/combat_icon_sheet_12.png`: doce iconos para arma, municion, vida, mana, dash, overdrive, jefe, peligros, escudo y victoria.
- `Audio/SFX/`: efectos originales en WAV mono a 44.1 kHz para menu, jugador, jefe, peligros, pickups, overdrive y resultados.
- `Audio/Music/arena_ambient_loop.wav`: base ambiental sintetizada para la arena.

## Guia rapida de audio

| Archivo | Evento sugerido |
| --- | --- |
| `menu_confirm.wav` | confirmar una opcion |
| `menu_cancel.wav` | volver o cancelar |
| `player_shot.wav` | disparo del rifle |
| `player_damage.wav` | recibir dano |
| `player_dash.wav` | dodge o dash |
| `player_reload.wav` | recarga |
| `boss_emerge.wav` | salir del suelo |
| `boss_telegraph.wav` | telegrapho de ataque |
| `boss_projectile.wav` | proyectil del jefe |
| `rock_impact.wav` | impacto de roca |
| `laser_warning.wav` | aviso de laser |
| `boss_phase_transition.wav` | entrada en fase dos |
| `boss_defeat.wav` | derrota del jefe |
| `pickup_health.wav` | recoger vida |
| `pickup_mana.wav` | recoger mana |
| `overdrive_activate.wav` | activar Overdrive |
| `victory_sting.wav` | victoria |
| `defeat_sting.wav` | derrota |

## Notas de integracion

Las laminas PNG son arte de concepto y fuentes para UI/retratos. No son modelos 3D riggeados ni sprites animados finales. Para usarlas como sprites, importa cada lamina en Unity y recorta sus elementos desde el Sprite Editor, o genera recortes individuales manteniendo la misma guia visual.

El script `generate_sfx_pack.py` permite regenerar el audio original si se necesitan nuevas duraciones o frecuencias. Unity creara los archivos `.meta` al importar la carpeta.
