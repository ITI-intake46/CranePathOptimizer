# AI Assist Slim Section Rail

This patch refines the docked AI Assist surface from a wide rail/menu into a scrollbar-sized vertical navigation spine.

## UX contract

- The AI dock keeps the compact state header, primary recommendation, focused section content, fixed prompt composer, and footer actions.
- The section rail is a narrow `34 px` column, close to the footprint of a professional scrollbar rather than a side menu.
- Rail labels are rotated vertically: `CHAT`, `MISSION`, `BIM`, `GA`, `REPORT`, and `STUDIO`.
- The active rail item uses the same orange accent language as the main ribbon tabs.
- The rail extends from the primary recommendation area through the fixed prompt composer, giving the AI dock a continuous navigation spine.
- Hovering over the rail and using the mouse wheel moves between sections without scrolling the section content.
- Hovering over the selected section content and using the mouse wheel scrolls only that content.
- The visible scrollbar remains hidden so the rail feels like intentional AI navigation rather than a generic scroll list.

## Preserved functionality

All existing AI capabilities remain available inside the focused sections:

- Chat with private live project context.
- Mission Control root cause, readiness, blocker evidence, and next step.
- BIM Intelligence count/category/building-type prompts and deterministic BIM Q&A.
- Optimization readiness, GA blocker guidance, and what-if advisor.
- Report & QA recovery language, report narrative, and evidence pack copying.
- AI Studio capability showcase, demo prompts, local RAG/ModelOps story, and decision trace.

## Why this design

The earlier section rail was functionally correct, but visually too wide. It read as another side menu inside the dock. The slim section rail preserves discoverability while giving the panel a calmer professional feel that is closer to CAD/BIM docked tooling: focused content, one primary action, and a compact navigation spine.
