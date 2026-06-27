import os
import pandas as pd
import json

def load_results(csv_path: str) -> pd.DataFrame:
    """Reads ExperimentResults.csv with utf-8-sig (BOM-safe) and casts types."""
    if not os.path.exists(csv_path):
        raise FileNotFoundError(f"Results CSV not found at: {csv_path}")
    
    df = pd.read_csv(csv_path, encoding="utf-8-sig")
    
    # Cast Datetime
    df["Timestamp"] = pd.to_datetime(df["Timestamp"])
    
    # Cast Floats
    float_cols = [
        "Coverage", "Fitness", "AverageRadius", "MaximumRadius", 
        "RelocationDistance", "FailureDominantPercent", "MutationRate",
        "MutationSigma", "CrossoverRate", "MinimumRelocationDistance", "GridStep"
    ]
    for col in float_cols:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors='coerce').astype("float64")
            
    # Cast Ints
    int_cols = [
        "PopulationSize", "MaxGenerations", "CoveredElements", "TotalElements",
        "ExecutionTimeMs", "GenerationReached", "UniqueCranePositions", "LiftFailures",
        "OutOfRadius", "Overweight", "HeightFail", "SpecialCase", "NotCharted", "RandomSeed",
        "ElementCount"
    ]
    for col in int_cols:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors='coerce').fillna(0).astype("int64")
            
    # Cast Categories
    cat_cols = ["CraneModel", "ProjectFile", "ProjectScale", "FailureDominantReason", "BenchmarkVersion"]
    for col in cat_cols:
        if col in df.columns:
            df[col] = df[col].astype("category")
            
    # Compute derived failure metrics
    df["TotalFailures"] = df[["OutOfRadius", "Overweight", "HeightFail", "SpecialCase", "NotCharted"]].sum(axis=1)
    
    return df

def load_summary(csv_path: str) -> pd.DataFrame:
    """Reads ExperimentSummary.csv with utf-8-sig."""
    if not os.path.exists(csv_path):
        raise FileNotFoundError(f"Summary CSV not found at: {csv_path}")
    return pd.read_csv(csv_path, encoding="utf-8-sig")

def validate_integrity(df: pd.DataFrame) -> dict:
    """Checks data integrity and raises ValueError on critical failures."""
    summary = {}
    
    # 1. Row count check
    summary["total_rows"] = len(df)
    if len(df) != 972:
        raise ValueError(f"Data integrity error: Expected exactly 972 rows, found {len(df)}")
        
    # 2. Duplicate RunId check
    unique_run_ids = df["RunId"].nunique()
    summary["unique_run_ids"] = unique_run_ids
    if unique_run_ids != 972:
        raise ValueError(f"Data integrity error: Duplicate RunIds found! Unique count is {unique_run_ids}")
        
    # 3. Seed distribution
    seeds = set(df["RandomSeed"].unique())
    summary["unique_seeds"] = list(seeds)
    if seeds != {1, 42, 99}:
        raise ValueError(f"Data integrity error: Expected seeds {1, 42, 99}, found {seeds}")
        
    # 4. Crane distribution
    cranes = set(df["CraneModel"].unique())
    summary["unique_cranes"] = list(cranes)
    if cranes != {"STC800", "180Tons"}:
        raise ValueError(f"Data integrity error: Expected cranes STC800 and 180Tons, found {cranes}")
        
    # 5. Project distribution
    projects = set(df["ProjectFile"].unique())
    summary["unique_projects"] = list(projects)
    if len(projects) != 2:
        raise ValueError(f"Data integrity error: Expected 2 project files, found {len(projects)}: {projects}")
        
    # 6. Null counts
    nulls = df.isnull().sum().sum()
    summary["total_null_values"] = int(nulls)
    if nulls > 0:
        raise ValueError(f"Data integrity error: Found {nulls} null values in results.")
        
    # 7. Coverage range
    if not df["Coverage"].between(0.0, 1.0).all():
        raise ValueError("Data integrity error: Coverage values must be between 0.0 and 1.0")
        
    # 8. Fitness == Coverage check
    if not df["Fitness"].equals(df["Coverage"]):
        raise ValueError("Data integrity error: Fitness does not equal Coverage in all rows")
        
    return summary
