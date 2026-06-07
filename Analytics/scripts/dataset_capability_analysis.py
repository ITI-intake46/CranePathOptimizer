import os
import pandas as pd
from datetime import datetime

def run(df: pd.DataFrame, dataset_dir: str, reports_dir: str):
    """Detects available datasets, versions, and supported analyses."""
    os.makedirs(reports_dir, exist_ok=True)
    
    # 1. Detect available files in dataset directory
    files = os.listdir(dataset_dir)
    results_present = "ExperimentResults.csv" in files
    summary_present = "ExperimentSummary.csv" in files
    config_present = "GAExperimentConfig.json" in files
    
    # 2. Extract column metadata
    columns = list(df.columns)
    column_count = len(columns)
    
    # 3. Detect benchmark versions
    versions = list(df["BenchmarkVersion"].unique()) if "BenchmarkVersion" in df.columns else ["Unknown"]
    
    # 4. Determine enabled analyses based on column profiles
    enabled_analyses = []
    
    # Check GA parameters
    required_ga_params = ["PopulationSize", "MaxGenerations", "MutationRate", "MutationSigma"]
    has_ga_params = all(col in df.columns for col in required_ga_params)
    if has_ga_params:
        enabled_analyses.append({
            "name": "Parameter Sensitivity Analysis",
            "status": "ENABLED",
            "reason": f"All required GA parameter columns {required_ga_params} are present."
        })
        enabled_analyses.append({
            "name": "GA Importance Analysis (ANOVA / Random Forest)",
            "status": "ENABLED",
            "reason": "Parameter dimensions are available for variance decomposition."
        })
        enabled_analyses.append({
            "name": "Interaction Analysis",
            "status": "ENABLED",
            "reason": "Factorial variables are present to compute two-way interaction terms."
        })
    else:
        missing = [col for col in required_ga_params if col not in df.columns]
        enabled_analyses.append({
            "name": "Parameter Sensitivity Analysis",
            "status": "DISABLED",
            "reason": f"Missing variable columns: {missing}."
        })
        
    # Check stability
    if "RandomSeed" in df.columns and len(df["RandomSeed"].unique()) > 1:
        enabled_analyses.append({
            "name": "GA Stability Analysis",
            "status": "ENABLED",
            "reason": f"RandomSeed column exists with {len(df['RandomSeed'].unique())} unique seeds."
        })
    else:
        enabled_analyses.append({
            "name": "GA Stability Analysis",
            "status": "DISABLED",
            "reason": "RandomSeed column is missing or contains only 1 seed."
        })
        
    # Check failure details
    failure_cols = ["OutOfRadius", "Overweight", "HeightFail", "NotCharted"]
    has_failures = all(col in df.columns for col in failure_cols)
    if has_failures:
        enabled_analyses.append({
            "name": "Failure Mechanics Breakdown",
            "status": "ENABLED",
            "reason": f"Individual failure type buckets {failure_cols} are available."
        })
    else:
        enabled_analyses.append({
            "name": "Failure Mechanics Breakdown",
            "status": "DISABLED",
            "reason": f"Missing failure columns."
        })
        
    # Check clustering/solution modes
    clustering_cols = ["UniqueCranePositions", "RelocationDistance", "AverageRadius", "Coverage"]
    has_clustering = all(col in df.columns for col in clustering_cols)
    if has_clustering:
        enabled_analyses.append({
            "name": "Solution Archetype Clustering",
            "status": "ENABLED",
            "reason": f"Spatial and coverage metrics {clustering_cols} are present for multi-dimensional clustering."
        })
    else:
        enabled_analyses.append({
            "name": "Solution Archetype Clustering",
            "status": "DISABLED",
            "reason": "Missing spatial or coverage features."
        })
        
    # 5. Write Capability Report
    report_path = os.path.join(reports_dir, "DatasetCapabilityReport.md")
    with open(report_path, "w", encoding="utf-8") as f:
        f.write("# Dataset Capability & Analysis Profile\n\n")
        f.write(f"Generated on: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n\n")
        
        f.write("## 1. Local Dataset Inventory\n\n")
        f.write("| File Name | Status | Purpose |\n")
        f.write("|---|---|---|\n")
        f.write(f"| `ExperimentResults.csv` | {'PRESENT' if results_present else 'MISSING'} | Primary parametric sweep run-level records |\n")
        f.write(f"| `ExperimentSummary.csv` | {'PRESENT' if summary_present else 'MISSING'} | High-level summary of best results per crane |\n")
        f.write(f"| `GAExperimentConfig.json` | {'PRESENT' if config_present else 'MISSING'} | Schema and parametric sweep bounds source file |\n")
        f.write("\n")
        
        f.write("## 2. Detected Benchmark Properties\n\n")
        f.write(f"- **Benchmark Version(s):** {', '.join(versions)}\n")
        f.write(f"- **Columns Found:** {column_count} columns\n")
        f.write(f"- **Total Data Points:** {len(df)} rows\n")
        f.write(f"- **Available Project Scales:** {list(df['ProjectScale'].unique()) if 'ProjectScale' in df.columns else 'None'}\n")
        f.write(f"- **Available Crane Models:** {list(df['CraneModel'].unique()) if 'CraneModel' in df.columns else 'None'}\n")
        f.write("\n")
        
        f.write("## 3. Analysis Module Compatibility Matrix\n\n")
        f.write("| Analysis Module | Status | Rationale |\n")
        f.write("|---|---|---|\n")
        for analysis in enabled_analyses:
            f.write(f"| {analysis['name']} | {analysis['status']} | {analysis['reason']} |\n")
        f.write("\n")
        
        f.write("## 4. Column Inventory & Data Typing\n\n")
        f.write("| Column Name | Non-Null Count | Data Type | Sample Value |\n")
        f.write("|---|---|---|---|\n")
        for col in df.columns:
            non_null = df[col].notnull().sum()
            dtype = str(df[col].dtype)
            sample = df[col].iloc[0] if len(df) > 0 else "N/A"
            if isinstance(sample, float):
                sample_str = f"{sample:.5f}"
            else:
                sample_str = str(sample)
            f.write(f"| `{col}` | {non_null} | {dtype} | {sample_str} |\n")
            
    print(f"DatasetCapabilityReport.md generated successfully at: {report_path}")

    # 6. Write Audit Report
    audit_path = os.path.join(reports_dir, "DatasetAuditReport.md")
    with open(audit_path, "w", encoding="utf-8") as f:
        f.write("# Dataset Audit Report\n\n")
        f.write(f"Generated on: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n\n")
        
        f.write("## 1. Summary Integrity Audit\n\n")
        f.write(f"- **Total Row Count:** {len(df)} data rows (Expected: 972)\n")
        f.write(f"- **Duplicate Run ID Check:** {'PASSED' if df['RunId'].nunique() == len(df) else 'FAILED'} (Unique RunIds: {df['RunId'].nunique()})\n")
        
        null_count = df.isnull().sum().sum()
        f.write(f"- **Null Value Check:** {'PASSED' if null_count == 0 else 'FAILED'} ({null_count} null cells found)\n")
        
        coverage_check = df["Coverage"].between(0.0, 1.0).all()
        f.write(f"- **Coverage Boundary Check (0.0 to 1.0):** {'PASSED' if coverage_check else 'FAILED'}\n")
        
        fitness_check = df["Fitness"].equals(df["Coverage"])
        f.write(f"- **Fitness Equals Coverage Consistency:** {'PASSED' if fitness_check else 'FAILED'}\n\n")
        
        f.write("## 2. Factor Distributions\n\n")
        
        f.write("### Random Seed Distribution\n")
        f.write("| Random Seed | Row Count | Percentage |\n")
        f.write("|---|---|---|\n")
        for seed, count in df["RandomSeed"].value_counts().items():
            f.write(f"| {seed} | {count} | {count/len(df)*100.0:.2f}% |\n")
        f.write("\n")
        
        f.write("### Crane Model Distribution\n")
        f.write("| Crane Model | Row Count | Percentage |\n")
        f.write("|---|---|---|\n")
        for crane, count in df["CraneModel"].value_counts().items():
            f.write(f"| {crane} | {count} | {count/len(df)*100.0:.2f}% |\n")
        f.write("\n")
        
        f.write("### Project File Distribution\n")
        f.write("| Project File | Row Count | Percentage |\n")
        f.write("|---|---|---|\n")
        for proj, count in df["ProjectFile"].value_counts().items():
            f.write(f"| {proj} | {count} | {count/len(df)*100.0:.2f}% |\n")
        f.write("\n")
        
        f.write("### Project Scale Distribution\n")
        f.write("| Project Scale | Row Count | Percentage |\n")
        f.write("|---|---|---|\n")
        for scale, count in df["ProjectScale"].value_counts().items():
            f.write(f"| {scale} | {count} | {count/len(df)*100.0:.2f}% |\n")
        f.write("\n")

    print(f"DatasetAuditReport.md generated successfully at: {audit_path}")

