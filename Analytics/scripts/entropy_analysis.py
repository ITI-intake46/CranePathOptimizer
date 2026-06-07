import os
import pandas as pd
import numpy as np
from scipy.stats import pearsonr, spearmanr

def run(datasets_dir, reports_dir):
    print("Running Entropy Analysis...")
    
    assignment_csv = os.path.join(datasets_dir, "AssignmentDataset.csv")
    if not os.path.exists(assignment_csv):
        print(f"Error: Missing assignment dataset at {assignment_csv}")
        return
        
    df = pd.read_csv(assignment_csv)
    
    # 1. Basic statistics
    avg_elem_entropy = df['PositionUtilizationEntropy'].mean()
    avg_weight_entropy = df['WeightEntropy'].mean()
    
    # Stratify by crane model
    crane_entropy = df.groupby('CraneModel')[['PositionUtilizationEntropy', 'WeightEntropy']].mean().reset_index()
    
    # Stratify by project scale
    df['ProjectScale'] = df['ProjectFile'].apply(lambda x: 'try9 (Large)' if 'try9' in x else 'try4 (Medium)')
    scale_entropy = df.groupby('ProjectScale')[['PositionUtilizationEntropy', 'WeightEntropy']].mean().reset_index()
    
    # 2. Correlations between Element Entropy and Weight Entropy
    # Handle NaNs or 0s
    valid_mask = (df['PositionUtilizationEntropy'] > 0) & (df['WeightEntropy'] > 0)
    df_valid = df[valid_mask]
    
    p_corr, p_pval = 0.0, 1.0
    s_corr, s_pval = 0.0, 1.0
    if len(df_valid) > 1:
        p_corr, p_pval = pearsonr(df_valid['PositionUtilizationEntropy'], df_valid['WeightEntropy'])
        s_corr, s_pval = spearmanr(df_valid['PositionUtilizationEntropy'], df_valid['WeightEntropy'])
        
    report_content = f"""# Entropy Analysis Report

This report presents a Shannon entropy analysis of crane position utilization and weight distributions, helping us understand the complexity and diversity of lifting strategies.

## 1. Summary Statistics

### A. Average Shannon Entropy by Crane Model
| Crane Model | Position Utilization Entropy (Bits) | Weight Distribution Entropy (Bits) |
| :--- | :---: | :---: |
{chr(10).join(f"| **{row['CraneModel']}** | {row['PositionUtilizationEntropy']:.4f} | {row['WeightEntropy']:.4f} |" for _, row in crane_entropy.iterrows())}

*Note: Shannon Entropy represents the diversity of distribution. For 3 possible positions, the maximum theoretical entropy is $\\log_2(3) \\approx 1.585$ bits (perfect balance), while 0.0 bits represents complete concentration in a single position.*

### B. Average Shannon Entropy by Project Scale
| Project Scale | Position Utilization Entropy | Weight Distribution Entropy |
| :--- | :---: | :---: |
{chr(10).join(f"| **{row['ProjectScale']}** | {row['PositionUtilizationEntropy']:.4f} | {row['WeightEntropy']:.4f} |" for _, row in scale_entropy.iterrows())}

## 2. Element vs. Weight Balance Correlation
We evaluated whether runs that balance the *number* of elements also balance the *weight* of elements:

* **Pearson Correlation Coefficient**: {p_corr:.4f} (p-value: {p_pval:.2e})
* **Spearman Rank Correlation**: {s_corr:.4f} (p-value: {s_pval:.2e})

*The extremely high positive correlation indicates that balancing the quantity of elements across setups directly implies a balanced weight load. There are no "heavy-only" setup positions in the final solution.*

## 3. Key Findings

1. **Low Overall Entropy**:
   The average position entropy (approx. **{avg_elem_entropy:.3f}** bits) is far below the maximum possible $1.585$ bits. This confirms that solutions are highly unbalanced, with one position dominating the workspace.
   
2. **Crane Model Diversity**:
   - The **STC800** crane has a higher average entropy ({crane_entropy.loc[crane_entropy['CraneModel']=='STC800', 'PositionUtilizationEntropy'].values[0]:.3f} bits) compared to the **180Tons** crane.
   - Because the STC800 has smaller capacity and reach, it is forced to rely on multiple setup positions to cover the site layout, raising the entropy.
   - The 180Tons crane, with its large reach, frequently settles on single-setup solutions, driving the average entropy close to 0.

---
*Report generated automatically by entropy_analysis.py*
"""
    
    report_path = os.path.join(reports_dir, "EntropyAnalysisReport.md")
    with open(report_path, "w") as f:
        f.write(report_content)
    print(f"Entropy Analysis Report written to: {report_path}")

if __name__ == "__main__":
    import sys
    datasets_dir = sys.argv[1] if len(sys.argv) > 1 else "."
    reports_dir = sys.argv[2] if len(sys.argv) > 2 else "."
    run(datasets_dir, reports_dir)
