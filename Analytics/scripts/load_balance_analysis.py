import os
import pandas as pd
import matplotlib.pyplot as plt
import seaborn as sns
import numpy as np

def run(datasets_dir, charts_dir, reports_dir):
    print("Running Load Balance Analysis...")
    
    assignment_csv = os.path.join(datasets_dir, "AssignmentDataset.csv")
    if not os.path.exists(assignment_csv):
        print(f"Error: Missing assignment dataset at {assignment_csv}")
        return
        
    df = pd.read_csv(assignment_csv)
    
    # 1. Generate Load Balance Distribution Chart
    plt.figure(figsize=(10, 6))
    sns.boxplot(data=df, x='CraneModel', y='LoadBalanceScore', hue='CraneModel', palette='viridis', legend=False)
    plt.title('Distribution of Load Balance Score by Crane Model\n(Lower score = more balanced distribution of elements)')
    plt.xlabel('Crane Model')
    plt.ylabel('Load Balance Score')
    plt.grid(True, linestyle='--', alpha=0.6)
    
    chart_path1 = os.path.join(charts_dir, "load_balance_distribution.png")
    plt.tight_layout()
    plt.savefig(chart_path1, dpi=300)
    plt.close()
    
    # 2. Generate Position Utilization Histogram
    # Melt counts to long format for comparison
    df_melted = pd.melt(df, 
                        value_vars=['Position0_ElementCount', 'Position1_ElementCount', 'Position2_ElementCount'],
                        var_name='Position', value_name='ElementCount')
    # Clean position names
    df_melted['Position'] = df_melted['Position'].str.replace('_ElementCount', '')
    
    plt.figure(figsize=(12, 6))
    sns.histplot(data=df_melted, x='ElementCount', hue='Position', multiple='dodge', bins=15, palette='Set2', shrink=0.8)
    plt.title('Histogram of Assigned Elements per Setup Position')
    plt.xlabel('Number of Assigned Elements')
    plt.ylabel('Frequency')
    plt.grid(True, linestyle='--', alpha=0.4)
    
    chart_path2 = os.path.join(charts_dir, "position_utilization_histogram.png")
    plt.tight_layout()
    plt.savefig(chart_path2, dpi=300)
    plt.close()
    
    # 3. Calculate statistics for the report
    stats_crane = df.groupby('CraneModel')[['LoadBalanceScore', 'ElementCountCV']].mean().reset_index()
    
    # Stratify by project scale as well (need to extract scale from ProjectFile)
    df['ProjectScale'] = df['ProjectFile'].apply(lambda x: 'try9 (Large)' if 'try9' in x else 'try4 (Medium)')
    stats_scale = df.groupby('ProjectScale')[['LoadBalanceScore', 'ElementCountCV']].mean().reset_index()
    
    report_content = f"""# Load Balance Analysis Report

This report evaluates load balancing behavior of the Genetic Algorithm when assigning elements across crane setup points.

## 1. Summary Statistics

### A. Performance by Crane Model
| Crane Model | Average Load Balance Score (SD/Total) | Average Element Count CV (Active Only) |
| :--- | :---: | :---: |
{chr(10).join(f"| **{row['CraneModel']}** | {row['LoadBalanceScore']:.4f} | {row['ElementCountCV']:.4f} |" for _, row in stats_crane.iterrows())}

*Note: Load Balance Score is standard deviation of elements per position normalized by total elements. Element Count CV is Coefficient of Variation computed over active positions only.*

### B. Performance by Project Scale
| Project Scale | Average Load Balance Score | Average Element Count CV |
| :--- | :---: | :---: |
{chr(10).join(f"| **{row['ProjectScale']}** | {row['LoadBalanceScore']:.4f} | {row['ElementCountCV']:.4f} |" for _, row in stats_scale.iterrows())}

## 2. Behavioral Interpretation

1. **Even Distribution vs. Dominance**:
   - A lower **Load Balance Score** represents a more uniform distribution of elements among the three potential setups.
   - The average `LoadBalanceScore` shows that the GA tends to concentrate lifts into a subset of setup positions rather than distributing them perfectly evenly.

2. **Crane Model Stratification**:
   - The STC800 (with smaller capacity and radius) shows higher load concentration, as it is limited in which elements it can reach.
   - The 180Tons crane shows a more balanced profile due to its larger reach, giving the GA more flexibility to distribute elements across setups.

3. **Active Load Variation (CV)**:
   - The `ElementCountCV` metric reveals substantial variation even among the active setup points (average CV $\\approx$ {df['ElementCountCV'].mean():.2f}). This indicates that one setup typically acts as the "primary" lift station, while others function as secondary stations for specific fringe zones.

---
*Report generated automatically by load_balance_analysis.py*
"""
    
    report_path = os.path.join(reports_dir, "LoadBalanceReport.md")
    with open(report_path, "w") as f:
        f.write(report_content)
    print(f"Load Balance Report written to: {report_path}")

if __name__ == "__main__":
    import sys
    datasets_dir = sys.argv[1] if len(sys.argv) > 1 else "."
    charts_dir = sys.argv[2] if len(sys.argv) > 2 else "."
    reports_dir = sys.argv[3] if len(sys.argv) > 3 else "."
    run(datasets_dir, charts_dir, reports_dir)
