# Sample Projects Directory — CraneOptimizer
## Requirement R-06 Acceptance Fixtures

---

This directory contains pre-seeded sample database and project assets for evaluation, testing, and supervisor review.

### Contents

1. **`Sample_Crane_Catalog.db`**
   - SQLite catalog database containing pre-configured crane specifications and load charts.
   - Includes:
     - **Sany STC800** (80t capacity, 45m main boom, 11.8m-45m operating range).
     - **Liebherr LTM 1200-5.1** (200t capacity, 72m main boom, 13.2m-72m operating range).
   - This database is automatically generated on first launch if absent, or can be loaded directly from this location.

### Quick Start for Reviewers

1. Launch CraneOptimizer (`CraneOptimizer.UI.exe`).
2. When creating a project, select or import from this directory.
3. The catalog database `Sample_Crane_Catalog.db` contains fully populated load charts ready for GA optimization testing.
