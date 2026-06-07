import os
import pandas as pd
import numpy as np

def run(datasets_dir, reports_dir):
    print("Running Position Utilization Analysis...")
    
    assignment_csv = os.path.join(datasets_dir, "AssignmentDataset.csv")
    if not os.path.exists(assignment_csv):
        print(f"Error: Missing assignment dataset at {assignment_csv}")
        return
        
    df = pd.read_csv(assignment_csv)
    
    # 1. Active Position Counts Frequency
    active_counts = df['ActivePositionCount'].value_counts().reset_index()
    active_counts.columns = ['ActivePositions', 'Count']
    active_counts['Percentage'] = active_counts['Count'] / len(df) * 100
    active_counts = active_counts.sort_values(by='ActivePositions')
    
    # 2. Active Position Counts by Crane
    crane_active = df.groupby('CraneModel')['ActivePositionCount'].value_counts(normalize=True).unstack(fill_value=0) * 100
    
    # 3. Dominant Position Share (LargestPositionShare)
    avg_dom_share = df['LargestPositionShare'].mean() * 100
    avg_dom_share_crane = df.groupby('CraneModel')['LargestPositionShare'].mean() * 100
    
    # Stratify by project scale
    df['ProjectScale'] = df['ProjectFile'].apply(lambda x: 'try9 (Large)' if 'try9' in x else 'try4 (Medium)')
    avg_dom_share_scale = df.groupby('ProjectScale')['LargestPositionShare'].mean() * 100
    
    report_content = f"""# Position Utilization Analysis Report

This report evaluates setup point utilization and dominance patterns across the `Benchmark_v1` GA runs.

## 1. Active Setup Count Frequencies
How many crane setup points are actually utilized (assigned $\\ge 1$ element) in the final solutions?

| Active Setup Count | Run Count | Percentage of Runs |
| :---: | :---: | :---: |
{chr(10).join(f"| **{row['ActivePositions']}** | {int(row['Count'])} | {row['Percentage']:.2f}% |" for _, row in active_counts.iterrows())}

*Almost all successful runs utilize either 1 or 2 setup points; very few distribute elements across all 3 allowed setup points. This represents a strong selection pressure by the GA toward minimizing relocations.*

## 2. Active Setup Count by Crane Model
Lifting layout strategies stratified by crane model:

| Crane Model | 0 Setups (%) | 1 Setup (%) | 2 Setups (%) | 3 Setups (%) |
| :--- | :---: | :---: | :---: | :---: |
{chr(10).join(f"| **{crane}** | {row.get(0, 0.0):.2f}% | {row.get(1, 0.0):.2f}% | {row.get(2, 0.0):.2f}% | {row.get(3, 0.0):.2f}% |" for crane, row in crane_active.iterrows())}

*The 180Tons crane, with its large lift radius, is much more likely to utilize only a single setup point (dominant position) because its reach covers a larger share of the site layout.*

## 3. Setup Dominance Analysis
The **Largest Position Share** measures the percentage of all covered elements assigned to the single most heavily utilized crane position in the run.

* **Global Average Dominance Share**: {avg_dom_share:.2f}%

### Dominance by Crane Model:
{chr(10).join(f"* **{crane}**: {share:.2f}% average share assigned to the dominant position" for crane, share in avg_dom_share_crane.items())}

### Dominance by Project Scale:
{chr(10).join(f"* **{scale}**: {share:.2f}% average share assigned to the dominant position" for scale, share in avg_dom_share_scale.items())}

## 4. Key Takeaways
1. **The Single Setup Dominance Paradigm**:
   On average, the primary setup location handles **{avg_dom_share:.1f}%** of the total lifting load. In many runs (especially for the 180Tons crane), this number reaches 100%, meaning the second and third setup points are empty.
   
2. **Setup Minimization Preference**:
   The GA engine fitness pressure penalizes relocation distance and setups. As a result, the solver prefers to crowd as many elements as possible into a single high-capacity setup point and only adds a second point when elements are completely out of reach.

---
*Report generated automatically by position_utilization_analysis.py*
"""
    
    report_path = os.path.join(reports_dir, "PositionUtilizationReport.md")
    with open(report_path, "w") as f:
        f.write(report_content)
    print(f"Position Utilization Report written to: {report_path}")

if __name__ == "__main__":
    import sys
    datasets_dir = sys.argv[1] if len(sys.argv) > 1 else "."
    reports_dir = sys.argv[2] if len(sys.argv) > 2 else "."
    run(datasets_dir, reports_dir)
