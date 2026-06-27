# Documentation Audit Report

This report presents a comprehensive audit of the documentation system for the Mobile Crane Path Optimizer construction scheduling tool.

---

## 1. Full File Inventory

| File Path | Title/Purpose | Word Count (approx) |
| --- | --- | --- |
| [agent/agent-context.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/agent/agent-context.md) | Agent Context & System Reference | 561 |
| [agent/definition-of-done.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/agent/definition-of-done.md) | Definition of Done (DoD) | 271 |
| [agent/do-not-do.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/agent/do-not-do.md) | Agent Rules: "Do Not Do" Checklist | 329 |
| [agent/start-here.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/agent/start-here.md) | Mobile Crane Path Optimizer — Developer & AI Agent Onboarding Guide | 1777 |
| [architecture/assignment-engine.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/assignment-engine.md) | Assignment Engine - Hungarian Solver | 407 |
| [architecture/database.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/database.md) | Database Architecture | 573 |
| [architecture/engines.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/engines.md) | System Engines | 1114 |
| [architecture/fitness-function.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/fitness-function.md) | Fitness Function - Adaptive Relocation Weighting | 428 |
| [architecture/folder-structure.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/system-overview.md) | Directory & Folder Structure | 847 |
| [architecture/ga-design-decisions.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/ga-design-decisions.md) | Genetic Algorithm Design Decisions | 751 |
| [architecture/ga-settings.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/ga-settings.md) | Genetic Algorithm Configuration Settings | 257 |
| [architecture/optimization-engine.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/optimization-engine.md) | Optimization Engine - Multi-Stage GA Orchestrator | 294 |
| [architecture/rendering-strategy.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/rendering-strategy.md) | Architecture: Rendering Strategy & Element Status Coloring | 969 |
| [architecture/system-overview.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/system-overview.md) | System Architecture Overview | 809 |
| [coding-standards/clean-code.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/coding-standards/clean-code.md) | Coding Standards: Clean Code & Architecture | 313 |
| [coding-standards/dependency-injection.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/coding-standards/dependency-injection.md) | Coding Standards: Dependency Injection | 370 |
| [coding-standards/dto-rules.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/coding-standards/dto-rules.md) | Coding Standards: Data Transfer Objects (DTO) Rules | 254 |
| [coding-standards/mvvm.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/coding-standards/mvvm.md) | Coding Standards: MVVM Implementation | 292 |
| [coding-standards/naming-conventions.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/coding-standards/naming-conventions.md) | Coding Standards: Naming Conventions | 237 |
| [decisions/ADR-001-use-xbim.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/decisions/ADR-001-use-xbim.md) | ADR-001: Use xBIM for IFC Building Model Parsing | 238 |
| [decisions/ADR-002-use-sqlite.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/decisions/ADR-002-use-sqlite.md) | ADR-002: Use SQLite for Local Database & Persistence | 189 |
| [decisions/ADR-003-use-mvvm.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/decisions/ADR-003-use-mvvm.md) | ADR-003: Use MVVM Pattern for WPF UI Layer | 191 |
| [decisions/ADR-004-use-ga.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/decisions/ADR-004-use-ga.md) | ADR-004: Use Genetic Algorithms for Crane Placement | 222 |
| [decisions/ADR-005-project-file-format.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/decisions/ADR-005-project-file-format.md) | ADR-005: Use SQLite for Project File Format (`.craneopt`) | 248 |
| [domain/crane-engine.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/domain/crane-engine.md) | Domain: Crane Engine & Load Charts | 693 |
| [domain/ga-engine.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-engine-design.md) | Domain: Genetic Algorithm (GA) Optimization Engine | 1176 |
| [domain/ga-pipeline.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-engine-design.md) | Domain: Genetic Algorithm (GA) Execution Pipeline | 1227 |
| [domain/ga-reference.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-settings-guide.md) | Genetic Algorithm Optimization Reference Guide | 1985 |
| [domain/ifc-documentation-audit.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/domain/ifc.md) | IFC Documentation Verification Audit | 1204 |
| [domain/ifc.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/domain/ifc.md) | Domain: IFC Processing Workflow | 1193 |
| [domain/lifting-engine.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/domain/lifting-engine.md) | Domain: Lifting Engine | 794 |
| [domain/path-engine.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/domain/path-engine.md) | Domain: Path Engine (Planned Feature) | 265 |
| [domain/units.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/domain/units.md) | Unit System Documentation | 695 |
| [ga/fitness-weights.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/fitness-weights.md) | Genetic Algorithm Fitness Weights Tuning Guide | 940 |
| [ga/ga-settings-audit.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-settings-guide.md) | GASettings Technical Audit Report | 3549 |
| [ga/ga-settings-developer-guide.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-settings-guide.md) | GASettings Developer & Calibration Guide | 1105 |
| [limitations.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/project/limitations.md) | System Limitations & Assumptions | 602 |
| [pdf-load-chart-import.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/domain/pdf-load-chart-import.md) | Architectural boundaries | 982 |
| [project/documentation-audit.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/audit/DocsAuditReport.md) | Documentation Audit Report | 2724 |
| [project/documentation-coverage-report.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/audit/DocsAuditReport.md) | Documentation Coverage Report | 437 |
| [project/ga-architecture-review.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-engine-design.md) | Genetic Algorithm Architecture & Mathematical Review | 3014 |
| [project/ga-performance-assessment.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/project/roadmap.md) | Executive Summary | 3425 |
| [project/phase-findings.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/project/roadmap.md) | Phase 15 Research Findings & GA Validation Report | 272 |
| [project/refactoring-opportunities.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/project/refactoring-opportunities.md) | Technical Debt and Architecture Review Report | 2680 |
| [project/roadmap.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/project/roadmap.md) | Mobile Crane Path Optimizer - Project Roadmap | 441 |
| [project/scope.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/project/scope.md) | Project Scope: MVP & Exclusions | 671 |
| [project/vision.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/project/vision.md) | Project Vision: Mobile Crane Path Optimizer for Precast Buildings | 425 |
| [ui/colors.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ui/design-system.md) | UI Color Palette & Visual Coding | 241 |
| [ui/design-system.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ui/design-system.md) | UI Design System | 316 |
| [ui/direct-material-coloring-design.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ui/direct-material-coloring-design.md) | Direct Material Coloring Design | 1384 |
| [ui/ga-run-view-design-review.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ui/ga-run-view-design-review.md) | F8_GA Run View UI Design Review - Final Calibration Layout | 456 |
| [ui/reports-ribbon-tab.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ui/reports-ribbon-tab.md) | Reports Ribbon Tab — Documentation | 923 |
| [ui/resource-dictionaries.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ui/design-system.md) | UI Resource Dictionaries | 304 |
| [ui/ui-implementation-notes.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ui/ui-implementation-notes.md) | UI Implementation Notes — GA Parameter Help System & Run History Logger | 426 |
| [workflows/bug-fixing.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/workflows/bug-fixing.md) | Workflow: Bug Fixing | 265 |
| [workflows/code-review.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/workflows/code-review.md) | Workflow: Code Review | 249 |
| [workflows/feature-development.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/workflows/feature-development.md) | Workflow: Feature Development | 340 |


---

## 2. Duplicate & Overlap Analysis

### Group 1: Genetic Algorithm Settings

They all cover the parameters and properties of GASettings, including detailed calibration guides, ranges, and presets. Overlap includes MaxCranePositions, CoverageWeight, RelocationWeight, and SlewOffset variables.

- **File A:** [ga/ga-settings-audit.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-settings-guide.md)
- **File B:** [ga/ga-settings-developer-guide.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-settings-guide.md)
  - *Similarity:* 0.17
  - *Severity:* Medium

- **File A:** [ga/ga-settings-audit.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-settings-guide.md)
- **File B:** [domain/ga-reference.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-settings-guide.md)
  - *Similarity:* 0.44
  - *Severity:* High

- **File A:** [ga/ga-settings-audit.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-settings-guide.md)
- **File B:** [architecture/ga-settings.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/ga-settings.md)
  - *Similarity:* 0.37
  - *Severity:* High

- **File A:** [ga/ga-settings-developer-guide.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-settings-guide.md)
- **File B:** [domain/ga-reference.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-settings-guide.md)
  - *Similarity:* 0.70
  - *Severity:* High

- **File A:** [ga/ga-settings-developer-guide.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-settings-guide.md)
- **File B:** [architecture/ga-settings.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/ga-settings.md)
  - *Similarity:* 0.29
  - *Severity:* Medium

- **File A:** [domain/ga-reference.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-settings-guide.md)
- **File B:** [architecture/ga-settings.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/ga-settings.md)
  - *Similarity:* 0.39
  - *Severity:* High

### Group 2: Genetic Algorithm Engine & Pipeline Flow

They all cover the core mechanics of the GA loop, parent selection, uniform crossover, mutations, and the step-by-step pipeline from initialization to cover-set post-processing.

- **File A:** [domain/ga-engine.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-engine-design.md)
- **File B:** [domain/ga-pipeline.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-engine-design.md)
  - *Similarity:* 0.81
  - *Severity:* High

- **File A:** [domain/ga-engine.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-engine-design.md)
- **File B:** [architecture/ga-design-decisions.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/ga-design-decisions.md)
  - *Similarity:* 0.61
  - *Severity:* High

- **File A:** [domain/ga-pipeline.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-engine-design.md)
- **File B:** [architecture/ga-design-decisions.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/ga-design-decisions.md)
  - *Similarity:* 0.54
  - *Severity:* High

### Group 3: IFC Processing & Category Audits

They cover parsing IFC building models using xBIM, categorizing elements as PrecastElement, extracting mass and COG, and running normalization decision pipelines.

- **File A:** [domain/ifc.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/domain/ifc.md)
- **File B:** [domain/ifc-documentation-audit.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/domain/ifc.md)
  - *Similarity:* 0.60
  - *Severity:* High

### Group 4: UI Design, Colors, and Style Resources

They cover styling guides, terracotta palette hex values, resource dictionaries mapping, and XAML merged dictionaries structures.

- **File A:** [ui/design-system.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ui/design-system.md)
- **File B:** [ui/colors.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ui/design-system.md)
  - *Similarity:* 0.25
  - *Severity:* Medium

- **File A:** [ui/design-system.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ui/design-system.md)
- **File B:** [ui/resource-dictionaries.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ui/design-system.md)
  - *Similarity:* 0.35
  - *Severity:* High

- **File A:** [ui/colors.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ui/design-system.md)
- **File B:** [ui/resource-dictionaries.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ui/design-system.md)
  - *Similarity:* 0.27
  - *Severity:* Medium

### Group 5: Folder Structures & Overall System Architecture

They cover directory structures mapping, database layer context setups, and assembly project dependencies.

- **File A:** [architecture/folder-structure.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/system-overview.md)
- **File B:** [architecture/system-overview.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/system-overview.md)
  - *Similarity:* 0.94
  - *Severity:* High



---

## 3. Consolidation Recommendations

### Group 1: Genetic Algorithm Settings

- **Recommended Action:** Merge
- **Target File:** [ga/ga-settings-guide.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-settings-guide.md) [NEW]
- **Content to Migrate:** Merge variable audits, parameter calibration steps, and descriptions of the 4 new properties and Clone method into a single comprehensive config guide. Keep `architecture/ga-settings.md` as a simple, high-level API reference.
- **Files that reference deprecated files (need link updates):**
  - [agent/start-here.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/agent/start-here.md)

### Group 2: Genetic Algorithm Engine & Pipeline Flow

- **Recommended Action:** Merge
- **Target File:** [ga/ga-engine-design.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-engine-design.md) [NEW]
- **Content to Migrate:** Combine the step-by-step evolutionary pipeline diagrams from `ga-pipeline.md` with the mathematical formulas and operator descriptions from `ga-engine.md`.
- **Files that reference deprecated files (need link updates):**
  - [agent/start-here.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/agent/start-here.md)
  - [domain/ga-engine.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ga/ga-engine-design.md)

### Group 3: IFC Processing & Category Audits

- **Recommended Action:** Merge & Delete
- **Target File:** [domain/ifc.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/domain/ifc.md)
- **Content to Migrate:** Merge the material density check audit results from `ifc-documentation-audit.md` into `ifc.md` and delete the audit file.
- **Files that reference deprecated files (need link updates):**
  - None

### Group 4: UI Design, Colors, and Style Resources

- **Recommended Action:** Merge & Delete
- **Target File:** [ui/design-system.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/ui/design-system.md)
- **Content to Migrate:** Consolidate the terracotta brand color matrix from `colors.md` and resource dictionary structure from `resource-dictionaries.md` as sub-sections in `design-system.md`.
- **Files that reference deprecated files (need link updates):**
  - [agent/start-here.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/agent/start-here.md)
  - [project/documentation-audit.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/audit/DocsAuditReport.md)
  - [project/documentation-coverage-report.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/audit/DocsAuditReport.md)

### Group 5: Folder Structures & Overall System Architecture

- **Recommended Action:** Merge & Delete
- **Target File:** [architecture/system-overview.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/system-overview.md)
- **Content to Migrate:** Merge the file/directory tree mapping list from `folder-structure.md` into `system-overview.md`.
- **Files that reference deprecated files (need link updates):**
  - [agent/start-here.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/agent/start-here.md)
  - [project/documentation-audit.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/audit/DocsAuditReport.md)
  - [project/documentation-coverage-report.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/audit/DocsAuditReport.md)



---

## 4. Structural Gaps

We identified the following structural gaps where dedicated documentation is missing but required:

### 1. Lifting & Configuration Calibration Guide
- **Suggested File Path:** `docs/domain/lifting-calibration.md`
- **Why it is needed:** Explains how boom configurations, boom angle limits, and the `ConservativeStep` resolver should be configured or calibrated for specific crane setups (e.g., LTM 1050 vs LTM 1250) on construction sites.
- **Which existing files partially cover it:** 
  - [domain/crane-engine.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/domain/crane-engine.md)
  - [domain/lifting-engine.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/domain/lifting-engine.md)

### 2. Pathfinding & Relocation Engine (Travel Routes)
- **Suggested File Path:** `docs/domain/pathfinding-relocation.md`
- **Why it is needed:** Details the travel route planning (planned Dubins curves and A* grid maps) for cranes moving between relocation setups on site.
- **Which existing files partially cover it:**
  - [domain/path-engine.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/domain/path-engine.md)
  - [project/roadmap.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/project/roadmap.md)

### 3. Database Migration & Schema Evolution Guide
- **Suggested File Path:** `docs/architecture/database-migrations.md`
- **Why it is needed:** Explains how database migrations are created, validated, and applied to local `.craneopt` SQLite files using Entity Framework Core when schema updates occur.
- **Which existing files partially cover it:**
  - [architecture/database.md](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/docs/architecture/database.md)

---

## 5. Recommended Final Structure

Below is the clean target directory layout after executing the recommended merges and deletions:

```
docs/
├── agent/
│   ├── agent-context.md
│   ├── definition-of-done.md
│   ├── do-not-do.md
│   └── start-here.md
├── architecture/
│   ├── system-overview.md       (merged system-overview.md)
│   ├── database.md
│   ├── engines.md
│   ├── optimization-engine.md
│   ├── assignment-engine.md
│   └── rendering-strategy.md
├── decisions/
│   ├── ADR-001-use-xbim.md
│   ├── ADR-002-use-sqlite.md
│   ├── ADR-003-use-mvvm.md
│   ├── ADR-004-use-ga.md
│   └── ADR-005-project-file-format.md
├── domain/
│   ├── crane-engine.md
│   ├── lifting-engine.md
│   ├── ifc.md                   (merged ifc.md)
│   ├── units.md
│   ├── lifting-calibration.md   [NEW GAP]
│   └── pathfinding-relocation.md [NEW GAP]
├── ga/
│   ├── ga-engine-design.md      (merged ga-engine.md and ga-engine-design.md)
│   ├── ga-settings-guide.md     (merged ga-settings-developer-guide.md, ga-settings-audit.md, ga-settings-guide.md)
│   ├── fitness-function.md
│   └── fitness-weights.md
├── project/
│   ├── vision.md
│   ├── scope.md
│   ├── roadmap.md
│   └── refactoring-opportunities.md
├── ui/
│   ├── design-system.md         (merged colors.md and design-system.md)
│   ├── direct-material-coloring-design.md
│   ├── ga-run-view-design-review.md
│   ├── reports-ribbon-tab.md
│   └── ui-implementation-notes.md
└── workflows/
    ├── feature-development.md
    ├── bug-fixing.md
    └── code-review.md
```
