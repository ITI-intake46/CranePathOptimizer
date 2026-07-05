# AI Command Center UX

This document records the earlier command-center polish pass. It has now been superseded by the slimmer [AI Assist Sleek Section Navigator](./AI_ASSIST_SLEEK_SECTION_NAVIGATOR.md).

## Historical problem addressed

The original side panel exposed many AI features, but users had to scroll through a long vertical document to find the desired tool. The first command-center iteration fixed that with pinned quick actions and horizontal tool tabs.

## Superseded approach

The command-center model introduced:

- fixed header with live state, readiness, model/session, and count context;
- pinned quick actions for common AI tasks;
- tab strip for direct access to Chat, Mission, BIM, Optimize, Report, and AI Studio;
- selected-tab content only;
- fixed prompt composer and footer actions;
- Full view handoff for deep AI work.

## Current approach

The current dock removes the button-heavy pinned-action row and horizontal tab strip. It keeps the same AI capabilities but presents them as a minimal vertical section navigator:

- Chat;
- Mission Control;
- BIM Intelligence;
- Optimization Readiness;
- Report & QA;
- AI Studio.

Each section exposes its own focused tools and evidence. The state header, primary recommendation, prompt composer, and compact footer remain fixed.

## Design guarantees

Both designs are presentation-only. They do not relax deterministic safety gates, optimizer prerequisites, trusted-weight rules, load-chart evidence requirements, report-review boundaries, or context-freshness checks.
