import os
import sys

# Ensure scripts directory is in path
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import dataset_loader
import dataset_capability_analysis
import ga_stability_analysis
import parameter_sensitivity_analysis
import best_parameter_discovery
import crane_comparison_analysis
import failure_analysis
import ga_importance_analysis
import interaction_analysis
import solution_mode_analysis
import ga_run_mode_discovery
import recommendation_engine
import report_generator

# Import new Phase 13 modules
import diagnostics_audit
import assignment_dataset_builder
import load_balance_analysis
import position_utilization_analysis
import entropy_analysis
import weight_distribution_analysis
import behavior_coverage_analysis
import strategy_importance_analysis
import advanced_solution_mode_analysis
import lifting_strategy_analysis

def main():
    print("==================================================")
    print("Starting Mobile Crane Path Optimizer Analytics")
    print("==================================================")
    
    # Define directories relative to this file
    scripts_dir = os.path.dirname(os.path.abspath(__file__))
    analytics_dir = os.path.dirname(scripts_dir)
    
    datasets_dir = os.path.join(analytics_dir, "datasets")
    reports_dir = os.path.join(analytics_dir, "reports")
    charts_dir = os.path.join(analytics_dir, "charts")
    
    results_csv = os.path.join(datasets_dir, "ExperimentResults.csv")
    
    os.makedirs(reports_dir, exist_ok=True)
    os.makedirs(charts_dir, exist_ok=True)
    
    # 1. Load results and validate integrity
    print("\n[1/23] Loading and auditing dataset...")
    df = dataset_loader.load_results(results_csv)
    audit_summary = dataset_loader.validate_integrity(df)
    print(f"Data audit successful. Loaded {audit_summary['total_rows']} rows.")
    
    # 2. Run Dataset Capability Discovery
    print("\n[2/23] Running Dataset Capability Discovery...")
    dataset_capability_analysis.run(df, datasets_dir, reports_dir)
    
    # 3. Run GA Stability Analysis
    print("\n[3/23] Running GA Stability Analysis...")
    ga_stability_analysis.run(df, charts_dir, reports_dir)
    
    # 4. Run Parameter Sensitivity Analysis
    print("\n[4/23] Running Parameter Sensitivity Analysis...")
    parameter_sensitivity_analysis.run(df, charts_dir, reports_dir)
    
    # 5. Run Best Parameter Discovery
    print("\n[5/23] Running Best Parameter Discovery...")
    best_parameter_discovery.run(df, reports_dir)
    
    # 6. Run Crane Comparison Analysis
    print("\n[6/23] Running Crane Comparison Analysis...")
    crane_comparison_analysis.run(df, charts_dir, reports_dir)
    
    # 7. Run Lift Failure Mechanics Analysis
    print("\n[7/23] Running Lift Failure Mechanics Analysis...")
    failure_analysis.run(df, charts_dir, reports_dir)
    
    # 8. Run GA Parameter Variance Importance Analysis
    print("\n[8/23] Running GA Importance Analysis...")
    ga_importance_analysis.run(df, charts_dir, reports_dir)
    
    # 9. Run Parameter Interaction Analysis
    print("\n[9/23] Running Parameter Interaction Analysis...")
    interaction_analysis.run(df, charts_dir, reports_dir)
    
    # 10. Run Spatial Solution Modes Discovery (Old)
    print("\n[10/23] Running Spatial Solution Modes Clustering...")
    solution_mode_analysis.run(df, charts_dir, reports_dir)
    
    # --- PHASE 13 ASSIGNMENT ANALYSIS PIPELINE ---
    
    # 11. Run Diagnostics Audit
    print("\n[11/23] Running Diagnostics Audit...")
    diagnostics_audit.run(reports_dir)
    
    # 12. Run Assignment Dataset Builder (generates datasets)
    print("\n[12/23] Building Assignment Datasets (Run & Position-level)...")
    assignment_dataset_builder.run(datasets_dir, reports_dir)
    
    # 13. Run Load Balance Analysis
    print("\n[13/23] Running Load Balance Analysis...")
    load_balance_analysis.run(datasets_dir, charts_dir, reports_dir)
    
    # 14. Run Position Utilization Analysis
    print("\n[14/23] Running Position Utilization Analysis...")
    position_utilization_analysis.run(datasets_dir, reports_dir)
    
    # 15. Run Entropy Analysis
    print("\n[15/23] Running Entropy Analysis...")
    entropy_analysis.run(datasets_dir, reports_dir)
    
    # 16. Run Weight Distribution Analysis
    print("\n[16/23] Running Weight Distribution Analysis...")
    weight_distribution_analysis.run(datasets_dir, reports_dir)
    
    # 17. Run Behavior vs. Coverage Analysis
    print("\n[17/23] Running Behavior vs. Coverage Analysis...")
    behavior_coverage_analysis.run(datasets_dir, charts_dir, reports_dir)
    
    # 18. Run Strategy Importance Analysis (Random Forest)
    print("\n[18/23] Running Strategy Importance Analysis...")
    strategy_importance_analysis.run(datasets_dir, charts_dir, reports_dir)
    
    # 19. Run Advanced Solution Mode Discovery (K-Means Clustering)
    print("\n[19/23] Running Advanced Solution Mode Clustering...")
    advanced_solution_mode_analysis.run(datasets_dir, charts_dir, reports_dir)
    
    # 20. Run Lifting Strategy Profiling
    print("\n[20/23] Running Lifting Strategy Profiling...")
    lifting_strategy_analysis.run(datasets_dir, reports_dir)
    
    # --- SYNTHESIS & PRESETS PIPELINE (Phase 13 updated) ---
    
    # 21. Run GA Preset Modes Discovery (with new behavior presets)
    print("\n[21/23] Running GA Preset Modes Discovery...")
    ga_run_mode_discovery.run(df, reports_dir)
    
    # 22. Run Practical Recommendations Engine
    print("\n[22/23] Running Practical Recommendations Engine...")
    recommendation_engine.run(df, reports_dir)
    
    # 23. Run Synthesis Report Generator (ResearchFindings.md)
    print("\n[23/23] Synthesizing final findings report...")
    report_generator.run(df, reports_dir)
    
    print("\n==================================================")
    print("Analytics Pipeline Run Completed Successfully!")
    print(f"Generated Reports: {len(os.listdir(reports_dir))} files in {reports_dir}")
    print(f"Generated Charts: {len(os.listdir(charts_dir))} files in {charts_dir}")
    print("==================================================")
if __name__ == "__main__":
    main()
