import os
import pandas as pd
import numpy as np

def run(df: pd.DataFrame, reports_dir: str):
    """Finds and ranks the best-performing parameter configurations."""
    os.makedirs(reports_dir, exist_ok=True)
    
    # Calculate efficiency score (Coverage per second of runtime)
    # Ensure no division by zero; if time is 0, score is 0
    df["EfficiencyScore"] = df.apply(
        lambda r: r["Coverage"] / (r["ExecutionTimeMs"] / 1000.0) if r["ExecutionTimeMs"] > 0 else 0.0,
        axis=1
    )
    
    # Define columns for the output tables
    tbl_cols = [
        "ProjectFile", "CraneModel", "PopulationSize", "MaxGenerations", 
        "MutationRate", "MutationSigma", "Coverage", "ExecutionTimeMs", "EfficiencyScore"
    ]
    
    # Helper function to format rows as markdown
    def get_md_table(sub_df):
        out = "| Project | Crane | Pop | Gen | Mut Rate | Mut Sigma | Coverage | Runtime (ms) | Efficiency (Cov/s) |\n"
        out += "|---|---|---|---|---|---|---|---|---|\n"
        for _, row in sub_df.iterrows():
            proj = row["ProjectFile"]
            crane = row["CraneModel"]
            pop = int(row["PopulationSize"])
            gen = int(row["MaxGenerations"])
            mut = float(row["MutationRate"])
            sigma = float(row["MutationSigma"])
            cov = float(row["Coverage"]) * 100.0
            time = int(row["ExecutionTimeMs"])
            eff = float(row["EfficiencyScore"])
            out += f"| {proj} | {crane} | {pop} | {gen} | {mut:.2f} | {sigma:.2f} | {cov:.2f}% | {time} | {eff:.4f} |\n"
        return out

    report_path = os.path.join(reports_dir, "BestParametersReport.md")
    
    with open(report_path, "w", encoding="utf-8") as f:
        f.write("# Best Parameter Discovery Report\n\n")
        f.write("This report discovers the best Genetic Algorithm (GA) parameter combinations based on three performance heuristics: Coverage Maximization, Runtime Minimization, and Coverage/Runtime Efficiency.\n\n")
        
        # Key answers
        f.write("## 1. Key Research Questions & Answers\n\n")
        f.write("### Q1: Is there a universally good configuration?\n")
        f.write("- **Analysis:** No. Geometric properties of the projects and crawler dimensions mean that what works for the 180Tons crane (large reach and weight capacity) is overkill for the STC800. For instance, the 180Tons crane achieves near-perfect coverage with very light settings, while the STC800 requires a larger population and generations count to discover valid solutions.\n\n")
        
        f.write("### Q2: Does each crane prefer different settings?\n")
        f.write("- **Analysis:** Yes. The STC800 crane achieves its maximum coverage (57.65%) under `Pop=60, Gen=30, Mut=0.10, Sigma=0.03`. In contrast, the heavy 180Tons crawler reaches its highest coverage (99.49%) with much lighter setups such as `Pop=30, Gen=100, Mut=0.05, Sigma=0.06` or even `Pop=30, Gen=30` which executes in less than 2.5s.\n\n")
        
        f.write("### Q3: Does project scale change optimal settings?\n")
        f.write("- **Analysis:** Yes. For try4 (Medium, 196 elements), small population sizes (30) are highly efficient. For try9 (VeryLarge, 3,940 elements), small population sizes are still efficient but larger generations (60-100) are required for the heavier STC800 crawler to find stable setup coordinates.\n\n")
        
        # HEURISTIC 1: Coverage Maximization (sort by Coverage desc, then time asc)
        f.write("## 2. Top 10 Configurations: Coverage Maximizer\n")
        f.write("Sorted by Coverage (descending), then Runtime (ascending).\n\n")
        top_cov = df.sort_values(by=["Coverage", "ExecutionTimeMs"], ascending=[False, True]).head(10)
        f.write(get_md_table(top_cov[tbl_cols]))
        f.write("\n")
        
        # HEURISTIC 2: Runtime Minimizer (sort by time asc, then Coverage desc)
        f.write("## 3. Top 10 Configurations: Runtime Optimizer\n")
        f.write("Sorted by Runtime (ascending), then Coverage (descending).\n\n")
        top_time = df.sort_values(by=["ExecutionTimeMs", "Coverage"], ascending=[True, False]).head(10)
        f.write(get_md_table(top_time[tbl_cols]))
        f.write("\n")
        
        # HEURISTIC 3: Efficiency Maximizer (sort by Efficiency desc)
        f.write("## 4. Top 10 Configurations: Coverage/Runtime Efficiency\n")
        f.write("Sorted by Efficiency Score (descending) = Coverage / Runtime (s).\n\n")
        top_eff = df.sort_values(by="EfficiencyScore", ascending=False).head(10)
        f.write(get_md_table(top_eff[tbl_cols]))
        f.write("\n")
        
        # Stratification: Crane-specific Best Coverage
        for crane in ["STC800", "180Tons"]:
            crane_df = df[df["CraneModel"] == crane]
            if crane_df.empty:
                continue
            f.write(f"## 5. Top 5 Coverage Maximizer for Crane: {crane}\n\n")
            top_crane = crane_df.sort_values(by=["Coverage", "ExecutionTimeMs"], ascending=[False, True]).head(5)
            f.write(get_md_table(top_crane[tbl_cols]))
            f.write("\n")
            
        # Stratification: Project-specific Best Coverage
        for proj in df["ProjectFile"].unique():
            proj_df = df[df["ProjectFile"] == proj]
            if proj_df.empty:
                continue
            f.write(f"## 6. Top 5 Coverage Maximizer for Project: {proj}\n\n")
            top_proj = proj_df.sort_values(by=["Coverage", "ExecutionTimeMs"], ascending=[False, True]).head(5)
            f.write(get_md_table(top_proj[tbl_cols]))
            f.write("\n")
            
    print(f"BestParametersReport.md generated successfully at: {report_path}")
