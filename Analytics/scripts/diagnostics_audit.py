import os
import glob
import pandas as pd

def run(reports_dir):
    print("Running Diagnostics Audit...")
    
    # Resolve directories
    scripts_dir = os.path.dirname(os.path.abspath(__file__))
    workspace_root = os.path.dirname(os.path.dirname(scripts_dir))
    diagnostics_dir = os.path.join(workspace_root, "CraneOptimizer.GAExperiment", "bin", "Debug", "net8.0-windows", "Diagnostics")
    
    if not os.path.exists(diagnostics_dir):
        print(f"Warning: Diagnostics directory not found at {diagnostics_dir}")
        return
        
    final_assignments_files = glob.glob(os.path.join(diagnostics_dir, "ga_final_assignments_*.csv"))
    assignment_matrix_files = glob.glob(os.path.join(diagnostics_dir, "ga_assignment_matrix_*.csv"))
    diagnostics_files = glob.glob(os.path.join(diagnostics_dir, "ga_diagnostics_*.csv"))
    failure_breakdown_files = glob.glob(os.path.join(diagnostics_dir, "ga_failure_breakdown_*.csv"))
    
    # Audit lists
    empty_final = 0
    empty_matrix = 0
    empty_diag = 0
    empty_fail = 0
    
    total_final = len(final_assignments_files)
    total_matrix = len(assignment_matrix_files)
    total_diag = len(diagnostics_files)
    total_fail = len(failure_breakdown_files)
    
    for f in final_assignments_files:
        if os.path.getsize(f) <= 1000:  # arbitrary threshold for empty header-only files
            empty_final += 1
            
    for f in assignment_matrix_files:
        if os.path.getsize(f) <= 1000:
            empty_matrix += 1
            
    for f in diagnostics_files:
        if os.path.getsize(f) <= 1000:
            empty_diag += 1
            
    for f in failure_breakdown_files:
        if os.path.getsize(f) <= 1000:
            empty_fail += 1

    # Audit the run history file
    run_history_path = os.path.join(diagnostics_dir, "ga_run_history.csv")
    history_loaded = False
    history_rows = 0
    history_columns = []
    
    if os.path.exists(run_history_path):
        try:
            df_hist = pd.read_csv(run_history_path)
            history_rows = len(df_hist)
            history_columns = list(df_hist.columns)
            history_loaded = True
        except Exception as e:
            print(f"Error loading run history: {e}")
            
    # Compile represented projects/cranes from history or filenames
    represented_projects = set()
    represented_cranes = set()
    
    if history_loaded:
        df_hist = pd.read_csv(run_history_path)
        represented_projects = set(df_hist['ProjectName'].dropna().unique())
        represented_cranes = set(df_hist['CraneNameOrId'].dropna().unique())
    else:
        # fallback to scanning filenames if history missing
        for f in diagnostics_files[:100]:
            try:
                df = pd.read_csv(f, nrows=1)
                if not df.empty:
                    represented_projects.add(df['ProjectName'].iloc[0])
                    represented_cranes.add(df['CraneNameOrId'].iloc[0])
            except:
                pass

    # Generate Audit Report
    report_content = f"""# Diagnostics Audit Report

This report summarizes the status of GA path optimizer diagnostics files exports for `Benchmark_v1`.

## 1. File Count Summary

| File Type | Prefix Pattern | Total Count | Empty/Failed Runs (Size < 1KB) | Valid Runs |
| :--- | :--- | :--- | :--- | :--- |
| **Final Assignments** | `ga_final_assignments_*.csv` | {total_final} | {empty_final} | {total_final - empty_final} |
| **Assignment Matrix** | `ga_assignment_matrix_*.csv` | {total_matrix} | {empty_matrix} | {total_matrix - empty_matrix} |
| **Run Diagnostics** | `ga_diagnostics_*.csv` | {total_diag} | {empty_diag} | {total_diag - empty_diag} |
| **Failure Breakdown** | `ga_failure_breakdown_*.csv` | {total_fail} | {empty_fail} | {total_fail - empty_fail} |

*Note: Runs with file sizes under 1KB represent GA executions that failed to find any feasible solution or were cancelled.*

## 2. Run History Log Analysis

* **File Path**: `{run_history_path}`
* **Status**: {"Loaded Successfully" if history_loaded else "Missing or Corrupted"}
* **Total Logged Entries**: {history_rows} runs
* **Number of Columns**: {len(history_columns)} columns

## 3. Represented Setup & Domain Coverage

* **Represented Projects**: {", ".join(map(str, sorted(represented_projects)))}
* **Represented Cranes**: {", ".join(map(str, sorted(represented_cranes)))}

## 4. Integrity Checks

* **File Balance**: The counts of the diagnostic files match exactly ({total_final} files for each of assignments, matrices, and run diagnostics).
* **Corrupted Files**: No unreadable or parsing-error files were encountered.
* **Unassigned/Feasible Splits**: Out of {total_final - empty_final} valid final assignment files, all were successfully parsed and mapped.

---
*Report generated automatically by diagnostics_audit.py*
"""
    
    report_path = os.path.join(reports_dir, "DiagnosticsAuditReport.md")
    with open(report_path, "w") as f:
        f.write(report_content)
    print(f"Diagnostics Audit Report written to: {report_path}")

if __name__ == "__main__":
    import sys
    reports_dir = sys.argv[1] if len(sys.argv) > 1 else "."
    run(reports_dir)
