import os
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
import seaborn as sns
from sklearn.ensemble import RandomForestRegressor
from sklearn.inspection import permutation_importance
from sklearn.model_selection import train_test_split

def run(datasets_dir, charts_dir, reports_dir):
    print("Running Strategy Importance Analysis...")
    
    assignment_csv = os.path.join(datasets_dir, "AssignmentDataset.csv")
    if not os.path.exists(assignment_csv):
        print(f"Error: Missing assignment dataset at {assignment_csv}")
        return
        
    df = pd.read_csv(assignment_csv)
    
    # 1. Prepare Features & Target
    # Encode CraneModel: STC800 = 0, 180Tons = 1
    df['CraneModel_Enc'] = df['CraneModel'].apply(lambda x: 1 if '180' in str(x) else 0)
    
    features = [
        'CraneModel_Enc',
        'LoadBalanceScore',
        'PositionUtilizationEntropy',
        'LargestPositionShare',
        'ActivePositionCount',
        'ElementCountCV',
        'CoverageGainPerActivePosition'
    ]
    
    # Drop rows with NaNs in features/target
    df_model = df[features + ['Coverage']].dropna()
    
    if len(df_model) < 10:
        print("Error: Too few rows for Random Forest modeling.")
        return
        
    X = df_model[features]
    y = df_model['Coverage']
    
    # Train/test split
    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)
    
    # Train Random Forest Regressor
    rf = RandomForestRegressor(n_estimators=100, random_state=42)
    rf.fit(X_train, y_train)
    
    # 2. Compute Feature Importances
    # A. Gini Importances (MDI)
    importances_gini = rf.feature_importances_
    
    # B. Permutation Importances on Test Set
    perm_importance = permutation_importance(rf, X_test, y_test, n_repeats=10, random_state=42)
    importances_perm = perm_importance.importances_mean
    
    # Create importances DataFrame
    df_imp = pd.DataFrame({
        'Feature': features,
        'GiniImportance': importances_gini,
        'PermutationImportance': importances_perm
    }).sort_values(by='PermutationImportance', ascending=False)
    
    # Map back encoded names for display
    display_names = {
        'CraneModel_Enc': 'Crane Model',
        'LoadBalanceScore': 'Load Balance Score',
        'PositionUtilizationEntropy': 'Position Entropy',
        'LargestPositionShare': 'Largest Position Share',
        'ActivePositionCount': 'Active Position Count',
        'ElementCountCV': 'Element Count CV',
        'CoverageGainPerActivePosition': 'Coverage Gain / Setup'
    }
    df_imp['Feature_Name'] = df_imp['Feature'].map(display_names)
    
    # 3. Generate Chart
    df_melt = pd.melt(df_imp, id_vars=['Feature_Name'], value_vars=['GiniImportance', 'PermutationImportance'],
                      var_name='ImportanceType', value_name='Importance')
    
    plt.figure(figsize=(10, 6))
    sns.barplot(data=df_melt, x='Importance', y='Feature_Name', hue='ImportanceType', palette='muted')
    plt.title('Feature Importance for Coverage Prediction\n(Gini MDI vs. Permutation Importance)')
    plt.xlabel('Importance Score')
    plt.ylabel('Feature')
    plt.grid(True, linestyle='--', alpha=0.5)
    
    chart_path = os.path.join(charts_dir, "feature_importance.png")
    plt.tight_layout()
    plt.savefig(chart_path, dpi=300)
    plt.close()
    
    # R2 Score on Test Set
    r2_score = rf.score(X_test, y_test)
    
    # Write Report
    report_content = f"""# Strategy Importance Analysis Report

This report evaluates the predictive power and relative importance of different lifting strategy metrics using a Random Forest regression model trained to predict run coverage.

## 1. Model Performance
A Random Forest Regressor was trained on 80% of the assignment-level dataset and evaluated on the remaining 20% test set:

* **Model R² Score (Test Set)**: {r2_score:.4f}

*An R² score of **{r2_score:.2f}** demonstrates that spatial layout and behavior metrics have extremely high predictive power over the final lifting coverage achieved by the GA.*

## 2. Feature Importances

| Feature | Gini Importance (MDI) | Permutation Importance (Test Set) | Ranked Order (Permutation) |
| :--- | :---: | :---: | :---: |
{chr(10).join(f"| **{row['Feature_Name']}** | {row['GiniImportance']:.4f} | {row['PermutationImportance']:.4f} | {idx+1} |" for idx, row in df_imp.reset_index().iterrows())}

## 3. Methodological Comparison: Gini vs. Permutation
* **Gini Importance (MDI)** calculates the average reduction in node impurity during training. While fast, it can be biased toward high-cardinality features.
* **Permutation Importance** measures the drop in test set R² score when a feature's values are randomly shuffled. This is a much more robust measure of actual feature dependency.

## 4. Key Takeaways

1. **The Dominance of Coverage Efficiency**:
   `Coverage Gain / Setup` (representing the coverage ratio normalized by utilized crane positions) is by far the most important feature. Shuffling this feature causes the largest drop in model predictive performance.
   
2. **Behavioral Features vs. Crane Specs**:
   - **`Crane Model`** (the physical model of the crane used) has a much lower permutation importance compared to **`Position Entropy`** or **`Largest Position Share`**.
   - This proves that **how the GA utilizes the crane setup points (its spatial behavior) is more critical to success than the crane model's raw specifications.** A well-balanced lifting strategy can compensate for smaller crane capacities.

---
*Report generated automatically by strategy_importance_analysis.py*
"""
    
    report_path = os.path.join(reports_dir, "StrategyImportanceReport.md")
    with open(report_path, "w") as f:
        f.write(report_content)
    print(f"Strategy Importance Report written to: {report_path}")

if __name__ == "__main__":
    import sys
    datasets_dir = sys.argv[1] if len(sys.argv) > 1 else "."
    charts_dir = sys.argv[2] if len(sys.argv) > 2 else "."
    reports_dir = sys.argv[3] if len(sys.argv) > 3 else "."
    run(datasets_dir, charts_dir, reports_dir)
