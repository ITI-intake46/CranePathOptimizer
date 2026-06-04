# Domain: Genetic Algorithm (GA) Execution Pipeline

This document describes the complete execution pipeline of the Genetic Algorithm (GA) optimizer in the Mobile Crane Path Optimizer.

---

## 1. Full Run Optimization Flow

```mermaid
graph TD
    %% User Trigger & Scope Building
    User[User Triggers Run] --> InputBuilder[OptimizationInputBuilder.Build]
    
    subgraph Input Validation & Settings Mutation
        InputBuilder --> ValElements{Elements Valid?<br/>IncludeInGa=true & weight > 0}
        ValElements -- No --> FailExit[Return Failure Result<br/>User-Facing Message]
        ValElements -- Yes --> ValSpecs{Cranes Valid?<br/>Load Chart present & MaxRadius > 0}
        
        ValSpecs -- No --> FailExit
        ValSpecs -- Yes --> ValZones{Zones Valid?<br/>At least 1 Allowed Zone}
        
        ValZones -- No --> FailExit
        ValZones -- Yes --> MutateSettings[Mutate Settings:<br/>MaxRadiusForNormalisation, DeratingRatio, CraneFootprintRadius]
        MutateSettings --> PassInput[Build PlacementInput Bundle]
    end
    
    %% Placement Orchestration Path
    PassInput --> RunOpt[CranePlacementEngine.RunOptimization]
    RunOpt --> CheckConcurrent{Settings.EnableConcurrentOptimization?}
    
    %% Legacy Path
    CheckConcurrent -- False --> SetCover[Legacy Greedy Set-Cover Loop<br/>Max 4 iterations or target coverage reached]
    SetCover --> GAEngineLegacy[GAEngine.Run]
    
    %% Concurrent Path
    CheckConcurrent -- True --> GAEngineConcurrent[GAEngine.Run]
    
    %% GA Initialization
    subgraph GA Core Solver (GAEngine.Run)
        GAEngineConcurrent --> GAInit[GAInitializer.InitializePopulation]
        GAInit --> PopulationMix[70% Random Samples<br/>30% Heuristic Morton Curve Centroids]
        PopulationMix --> TrackGen[Track GAProfiler:<br/>CandidatePointsGenerated, ValidPointsGenerated]
        
        %% GA Generation Loop
        TrackGen --> GenLoop{Generation Loop<br/>0 to MaxGenerations}
        GenLoop -- Loop --> EvalPop[EvaluatePopulation: foreach solution]
        
        subgraph Solution Assignment & Scoring
            EvalPop --> AssignService[ElementAssignmentService.EvaluateAssignment<br/>Using LoadChartIndex & ElementStaticCache]
            AssignService --> CalcRadius[Calculate Reach Radius R]
            CalcRadius --> CalcHeight[Calculate Lift Height H]
            CalcHeight --> CalcBoom[Calculate Required Boom Length B]
            CalcBoom --> ResolveChart[LoadChartCapacityResolver.Resolve]
            ResolveChart --> FitnessEvaluator[FitnessEvaluator.EvaluateFitness]
        end
        
        FitnessEvaluator --> SelectParents[TournamentSelection.SelectParents<br/>Size k=3]
        SelectParents --> Crossover[CrossoverOperator.Crossover]
        Crossover --> Mutate[MutationOperator.Mutate<br/>80% Perturb / 10% Add / 10% Remove]
        Mutate --> Sanitize[ConstraintEngine.SanitizeChromosome / FilterDistinctValidPoints]
        Sanitize --> Replace[ReplacementStrategy.Replace]
        
        Replace --> CheckStagnation{Stagnant for EarlyStoppingGenerations?}
        CheckStagnation -- Yes --> EndLoop[Stop Loop & Return Best CraneSolution]
        CheckStagnation -- No --> GenLoop
    end
    
    %% Mapping Result & Deduplication
    EndLoop --> MapResult[CranePlacementEngine.MapResult]
    GAEngineLegacy --> MapResult
    
    subgraph Result Formatting & Post-Processing
        MapResult --> ReEvaluate[Re-evaluate final positions]
        ReEvaluate --> ResolveAssignments[AssignmentPolicy.ResolveFinalAssignments<br/>Unique element-crane ownership]
        ResolveAssignments --> RecalcCoverage[CoverageEngine.RecalculateCoverage]
        RecalcCoverage --> Deduplicate[CoverageEngine.DeduplicateFinalPositions]
        Deduplicate --> BuildResult[Build PlacementResult with statistics]
    end
    
    BuildResult --> FinalOutput[Final Output Result Visualized in Viewport]
```

The classes and methods coordinating this pipeline are:
*   **Input Scope Builder:** [OptimizationInputBuilder.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/OptimizationScope/OptimizationInputBuilder.cs) (Class: `OptimizationInputBuilder`, Method: `Build`)
*   **Optimization Orchestrator:** [CranePlacementEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/PlacementOrchestration/CranePlacementEngine.cs) (Class: `CranePlacementEngine`, Method: `RunOptimization`)
*   **GA Core Solver:** [GAEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAEngine.cs) (Class: `GAEngine`, Method: `Run`)
*   **Population Initializer:** [GAInitializer.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/GAInitializer.cs) (Class: `GAInitializer`, Method: `InitializePopulation`)
*   **Assignment Evaluator:** [ElementAssignmentService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/ElementAssignmentService.cs) (Class: `ElementAssignmentService`, Method: `EvaluateAssignment`)
*   **Fitness Evaluator:** [FitnessEvaluator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs) (Class: `FitnessEvaluator`, Method: `EvaluateFitness`)
*   **Selection Operator:** [TournamentSelection.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/TournamentSelection.cs) (Class: `TournamentSelection`, Method: `SelectParents`)
*   **Crossover Operator:** [CrossoverOperator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/CrossoverOperator.cs) (Class: `CrossoverOperator`, Method: `Crossover`)
*   **Mutation Operator:** [MutationOperator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/MutationOperator.cs) (Class: `MutationOperator`, Method: `Mutate`)
*   **Sanitization & Constraints:** [ConstraintEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/Constraints/ConstraintEngine.cs) (Class: `ConstraintEngine`, Method: `FilterDistinctValidPoints` / `IsValidSetupPoint`)
*   **Replacement Strategy:** [ReplacementStrategy.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Operators/ReplacementStrategy.cs) (Class: `ReplacementStrategy`, Method: `Replace`)
*   **Final Policy & Coverage:** [AssignmentPolicy.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/AssignmentPolicy.cs) (Class: `AssignmentPolicy`, Method: `ResolveFinalAssignments`) and [CoverageEngine.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/Coverage/CoverageEngine.cs) (Class: `CoverageEngine`, Methods: `DeduplicateFinalPositions`, `RecalculateCoverage`, and `FilterRemainingElements`).

---

## 2. Element Assignment Decision Tree

For every precast element evaluated against every crane setup candidate coordinate in a chromosome, the feasibility is determined step-by-step. The logic is implemented in `ElementAssignmentService.ComputeEvaluationResult` and resolved via `LoadChartCapacityResolver.Resolve`.

```
For each (element, craneSetupPosition) pair:
│
├── 1. Calculate R = OperatingRadius (accounting for SlewOffsetX/Y offsets)
│    └── R < MinRadius OR R > MaxRadius?
│         ├── YES ──> [Status: OutOfRadius]
│         └── NO
│              │
│              ├── 2. Calculate H = LiftHeight (accounting for half-height and WireLength)
│              ├── 3. Calculate Required Boom Length B (accounting for hinge offset)
│              │    └── B > MaxBoomLength?
│              │         ├── YES ──> [Status: HeightFail]
│              │         └── NO
│              │              │
│              │              └── 4. Query ConservativeStep load chart (with pre-sorted LoadChartIndex lookup):
│              │                   ├── No listed chart radii where radius >= R?
│              │                   │    └── YES ──> [Status: OutsideChartEnvelope]
│              │                   │
│              │                   ├── No listed boom configurations where boom length >= B at selected radius?
│              │                   │    └── YES ──> [Status: OutsideChartEnvelope]
│              │                   │
│              │                   ├── Lowest positive capacity entry resolved in boom group:
│              │                   │    ├── Capacity cell <= -1? (Empty/blank)
│              │                   │    │    └── YES ──> [Status: InvalidBlankCell (NotCharted)]
│              │                   │    │
│              │                   │    ├── Capacity cell == 0? (Zero capacity)
│              │                   │    │    └── YES ──> [Status: InvalidZeroCell (NotCharted)]
│              │                   │    │
│              │                   │    └── Positive Capacity resolved:
│              │                   │         │
│              │                   │         ├── Calculate AvailableCapacity = ChartCapacity × DeratingRatio × SafetyFactor
│              │                   │         │
│              │                   │         └── Is AvailableCapacity >= RequiredWeight?
│              │                   │              ├── YES ──> [Status: Liftable] ✓
│              │                   │              └── NO  ──> [Status: Overweight]
```

*   **Status Codes:** Mapped to the `LiftStatus` enum inside [LiftStatus.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models/LiftStatus.cs).
*   **Capacity Errors:** Mapped via the `CapacityFailureReason` enum inside [LoadChartResolverResult.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Models/LoadChartResolverResult.cs).

---

## 3. Fitness Formulation & Penalization Metrics

The core GA selection pressure is driven by the fitness formula implemented in [FitnessEvaluator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Engines/GeneticOptimization/FitnessEvaluator.cs) (Method: `EvaluateFitness`).

### Core Deductions and Calculations

1.  **Coverage Ratio ($R_{\text{cov}}$):**
    The positive signal of the GA. Calculated using the count of unique liftable element IDs divided by total elements:
    $$R_{\text{cov}} = \frac{\text{uniqueLiftableCount}}{\text{total}}$$
    *   **Zero Coverage Threshold:** If $\text{uniqueLiftableCount} = 0$, fitness is immediately resolved as $0$ to bypass futile selection evaluations.

2.  **Relocation Penalty ($\text{Penalty}_{\text{rel}}$):**
    Instead of scaling by chromosome capacities, the raw fitness deduction is step-wise based on the number of active crane setups:
    $$\text{Penalty}_{\text{rel}} = \text{settings.RelocationWeight} \times (n - 1)$$
    where $n = \text{craneCount}$. (A single setup has 0 relocation penalty; each additional setup adds a full weight penalty).

3.  **Failure Fractions:**
    To normalize scale across projects, unliftable elements are counted and divided by the total number of elements in the current pass (`total`):
    *   **Out of Radius Fraction:** $P_{\text{rad}} = \frac{\text{outOfRadiusCount}}{\text{total}}$
    *   **Overweight Fraction:** $P_{\text{ow}} = \frac{\text{overweightCount}}{\text{total}}$
    *   **Height Failure Fraction:** $P_{\text{h}} = \frac{\text{heightFailCount}}{\text{total}}$
    *   **Special Case / Uncharted Fraction:** $P_{\text{sc}} = \frac{\text{specialCaseCount}}{\text{total}}$

4.  **Complete Fitness Equation:**
    $$\text{Fitness} = \max\left(0, W_{\text{cov}} \times R_{\text{cov}} - \text{Penalty}_{\text{rel}} - W_{\text{rad}} \times P_{\text{rad}} - W_{\text{ow}} \times P_{\text{ow}} - W_{\text{h}} \times P_{\text{h}} - W_{\text{sc}} \times P_{\text{sc}} - W_{\text{rq}} \times P_{\text{rq}} - P_{\text{bal}} - P_{\text{sd}} - W_{\text{sim}} \times P_{\text{sim}} - W_{\text{dist}} \times P_{\text{dist}}\right)$$

    *   **Double Penalization Caveat:** Under this formula, any unliftable element decreases positive $R_{\text{cov}}$ *and* simultaneously increases its corresponding failure fraction (e.g. $P_{\text{rad}}$). This creates a steep fitness gradient to quickly weed out unfeasible coordinates.
