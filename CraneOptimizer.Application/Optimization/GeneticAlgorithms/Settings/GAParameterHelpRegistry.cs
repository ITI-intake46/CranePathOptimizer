using System;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;

public static class GAParameterHelpRegistry
{
    private static readonly Dictionary<string, ParameterHelpMetadata> _registry = new(StringComparer.OrdinalIgnoreCase);

    static GAParameterHelpRegistry()
    {
        // 1. Core Calibration
        Add(new ParameterHelpMetadata
        {
            ParameterKey = "PopulationSize",
            DisplayName = "Population Size",
            Description = "The number of candidate solutions (chromosomes) maintained in each generation.",
            IncreaseEffect = "Provides a broader search of the layout space, reducing the risk of premature convergence.",
            DecreaseEffect = "Reduces execution time and memory footprint per generation.",
            ValidRange = "1 or more",
            RecommendedRange = "50 – 300",
            PerformanceImpact = "High — directly scales memory allocation and fitness evaluation times.",
            QualityImpact = "High — small populations struggle to find global optima in complex layouts.",
            SpecialNote = "Setting below 20 may lead to premature stagnation on large sites."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "MaxGenerations",
            DisplayName = "Max Generations",
            Description = "The maximum number of evolutionary loops the algorithm will run before stopping.",
            IncreaseEffect = "Allows the algorithm more iterations to refine placement coordinates and converge.",
            DecreaseEffect = "Forces the solver to terminate earlier, ensuring faster response times.",
            ValidRange = "1 or more",
            RecommendedRange = "100 – 1000",
            PerformanceImpact = "High — scales the duration of the solver run.",
            QualityImpact = "High — terminating too early prevents the GA from finding fully converged results."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "MutationRate",
            DisplayName = "Mutation Rate",
            Description = "The probability that a candidate solution's crane coordinates undergo random perturbation or structural adjustment.",
            IncreaseEffect = "Promotes spatial exploration, helping the population escape local optima.",
            DecreaseEffect = "Focuses the search on local exploitation, preserving structure.",
            ValidRange = "0.0 to 1.0",
            RecommendedRange = "0.05 – 0.20",
            PerformanceImpact = "Low — minor overhead from random number generation.",
            QualityImpact = "High — too high turns search into a random walk; too low leads to early convergence."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "MutationSigma",
            DisplayName = "Mutation Sigma",
            Description = "The standard deviation of the Gaussian noise applied to coordinates during mutation.",
            IncreaseEffect = "Increases the step size of coordinate changes, encouraging wider exploration.",
            DecreaseEffect = "Refines coordinates in a local neighborhood, making fine adjustments.",
            ValidRange = "Greater than 0",
            RecommendedRange = "0.01 – 0.10 (ratio of normalization radius)",
            PerformanceImpact = "Low",
            QualityImpact = "Medium — balances local tuning against global exploration."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "MaxCranePositions",
            DisplayName = "Max Crane Positions",
            Description = "Maximum number of crane setup positions allowed. In multi-stage optimization, the solver tests 1 setup, then 2, up to this value, and returns the best valid result. The final result may use fewer positions.",
            IncreaseEffect = "Allows the optimizer to explore solutions with more crane relocations, potentially increasing coverage.",
            DecreaseEffect = "Restricts setups, forcing the solver to find a more compact single-location solution.",
            ValidRange = "1 to 10",
            RecommendedRange = "1 – 4",
            PerformanceImpact = "High — directly increases chromosome size and set-cover complexity.",
            QualityImpact = "High — dominant factor in layout feasibility.",
            SpecialNote = "This is an upper limit, not a required count. The optimizer may return fewer positions if fewer setups achieve the coverage target. Early stopping occurs when the target coverage ratio is met."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "EarlyStoppingGenerations",
            DisplayName = "Early Stopping Generations",
            Description = "The number of consecutive generations without fitness improvement required to stop.",
            IncreaseEffect = "Allows more iterations to break out of temporary stagnation periods.",
            DecreaseEffect = "Speeds up solver runs by terminating early when progress flattens.",
            ValidRange = "5 or more",
            RecommendedRange = "20 – 50",
            PerformanceImpact = "Medium — affects total generation count.",
            QualityImpact = "Medium — too small can stop the GA before late-stage improvements occur."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "ConvergenceThresholdPercent",
            DisplayName = "Convergence Threshold (%)",
            Description = "Relative percentage improvement in fitness required to count as optimization progress. Set to 0 to disable.",
            IncreaseEffect = "Triggers earlier termination, saving computation time.",
            DecreaseEffect = "Forces the solver to run longer to find micro-optimizations.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "0.1% – 2.0% (0.0 to disable)",
            PerformanceImpact = "Medium — affects generation count.",
            QualityImpact = "Low to Medium — prevents spending runtime on negligible score updates."
        });

        // 2. Fitness Strategy
        Add(new ParameterHelpMetadata
        {
            ParameterKey = "CoverageWeight",
            DisplayName = "Coverage Weight",
            Description = "Deduction weight factor applied to the ratio of successfully liftable elements.",
            IncreaseEffect = "Prioritizes lifting as many elements as possible above all other objectives.",
            DecreaseEffect = "Balances coverage against relocation costs, movement distance, and radial deviation.",
            ValidRange = "Greater than 0",
            RecommendedRange = "10.0 – 50.0",
            PerformanceImpact = "None",
            QualityImpact = "Dominant — the primary term that determines solution quality.",
            SpecialNote = "This is the most critical weight. Keep it high to ensure element coverage."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "RelocationWeight",
            DisplayName = "Relocation Weight",
            Description = "Penalty for extra crane setup positions. In multi-stage optimization, this value is adapted internally per stage: the first stage uses no relocation penalty, and later stages scale the penalty based on previous coverage.",
            IncreaseEffect = "Favors solutions with fewer relocations to minimize setup mobilization cost.",
            DecreaseEffect = "Allows the solver to use more crane setups to cover difficult corners.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "2.0 – 10.0",
            PerformanceImpact = "None",
            QualityImpact = "High — models setup mobilization cost.",
            SpecialNote = "In multi-stage mode, this value is not used directly per stage. Stage 1 always uses 0. Later stages use an adaptive formula based on coverage achieved. This value acts as a scaling base for the adaptive calculation."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "RadiusPenaltyWeight",
            DisplayName = "Radius Penalty Weight",
            Description = "Deduction penalty scaled by the proportion of elements that are out of reach of all setups.",
            IncreaseEffect = "Aggressively penalizes setups that leave elements outside the operating radius.",
            DecreaseEffect = "Allows positions that miss elements if they have high overall score.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "3.0 – 15.0",
            PerformanceImpact = "None",
            QualityImpact = "High — ensures physical reachability."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "RadiusQualityPenaltyWeight",
            DisplayName = "Radius Quality Weight",
            Description = "Deduction penalty that encourages setups to be closer to elements, keeping operating radii short.",
            IncreaseEffect = "Forces setups closer to elements, increasing safety margin.",
            DecreaseEffect = "Allows setups to operate near their maximum radius limits.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "0.5 – 2.0",
            PerformanceImpact = "None",
            QualityImpact = "Medium — improves capacity reserves."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "BalancePenaltyWeight",
            DisplayName = "Balance Penalty Weight",
            Description = "Deduction penalty for solutions with highly uneven element distributions between setups.",
            IncreaseEffect = "Favors balanced work distribution across setups.",
            DecreaseEffect = "Allows a single setup to do the bulk of the lifting work.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "1.0 – 5.0",
            PerformanceImpact = "None",
            QualityImpact = "Medium"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "RadiusStdDevPenaltyWeight",
            DisplayName = "Radius StdDev Weight",
            Description = "Deduction penalty that penalizes high variance in lifting radii across all liftable elements.",
            IncreaseEffect = "Encourages consistent operating distances, simplifying work cycles.",
            DecreaseEffect = "Allows highly varied operating distances.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "1.0 – 5.0",
            PerformanceImpact = "None",
            QualityImpact = "Medium"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "SimilarPositionPenaltyWeight",
            DisplayName = "Similar Position Weight",
            Description = "Deduction penalty for setups placed closer than the Minimum Relocation Distance.",
            IncreaseEffect = "Forces setups to be spatially separated and distinct.",
            DecreaseEffect = "Allows setups to cluster close to each other.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "5.0 – 15.0",
            PerformanceImpact = "None",
            QualityImpact = "High — prevents redundant coordinate overlap."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "RelocationDistancePenaltyWeight",
            DisplayName = "Relocation Dist Weight",
            Description = "Deduction penalty scaled by the cumulative coordinate distance traveled between setups.",
            IncreaseEffect = "Favors setups that are close to each other, reducing travel movement.",
            DecreaseEffect = "Allows setups to be on opposite sides of the site.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "0.5 – 3.0",
            PerformanceImpact = "None",
            QualityImpact = "Medium"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "OverweightPenaltyWeight",
            DisplayName = "Overweight Weight",
            Description = "Deduction penalty applied for elements exceeding the rated capacity in load charts.",
            IncreaseEffect = "Ensures structural safety by rejecting solutions that attempt overload lifts.",
            DecreaseEffect = "Reduces capacity safety enforcement.",
            ValidRange = "Greater than 0",
            RecommendedRange = "10.0 – 50.0",
            PerformanceImpact = "None",
            QualityImpact = "High — critical safety constraint.",
            SpecialNote = "Keep this value high to guarantee capacity compliance."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "HeightPenaltyWeight",
            DisplayName = "Height Penalty Weight",
            Description = "Deduction penalty applied when an element exceeds the allowable hook clearance height.",
            IncreaseEffect = "Rejects solutions with boom configuration clearance violations.",
            DecreaseEffect = "Reduces boom height enforcement.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "5.0 – 20.0",
            PerformanceImpact = "None",
            QualityImpact = "High — critical vertical constraint."
        });

        // 3. Layout Constraints
        Add(new ParameterHelpMetadata
        {
            ParameterKey = "MinimumRelocationDistance",
            DisplayName = "Min Relocation Dist (m)",
            Description = "The minimum required distance in meters between two setups to count as unique relocations.",
            IncreaseEffect = "Spatially disperses setups across the jobsite.",
            DecreaseEffect = "Allows setups to be placed closer to each other.",
            ValidRange = "Greater than 0",
            RecommendedRange = "5.0 – 25.0",
            PerformanceImpact = "Low",
            QualityImpact = "High — coordinates closer than this trigger similarity penalties."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "GridStep",
            DisplayName = "Grid Step (m)",
            Description = "Snaps setup search points to a physical grid in meters. Set to 0 for free continuous placement.",
            IncreaseEffect = "Reduces search space size, accelerating initial random seeding.",
            DecreaseEffect = "Provides high precision, allowing continuous fine-tuning.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "0.5 – 5.0 (0 to disable)",
            PerformanceImpact = "Medium — snaps coordinates on mutation and seeding.",
            QualityImpact = "Medium"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "TargetElementTypes",
            DisplayName = "Target Element Types",
            Description = "Restricts the optimization target set to specific IFC element types (e.g. Columns, Slabs).",
            IncreaseEffect = "Expands the element set, increasing scope.",
            DecreaseEffect = "Filters element count, prioritizing specific building components.",
            ValidRange = "Collection of types",
            RecommendedRange = "Configured dynamically from model elements",
            PerformanceImpact = "High — directly scales evaluation complexity by element count.",
            QualityImpact = "High — restricts solver focus."
        });

        // 4. Engineering Model
        Add(new ParameterHelpMetadata
        {
            ParameterKey = "WireLength",
            DisplayName = "Wire Length (m)",
            Description = "Rigging wire length added to the calculated required vertical hook height.",
            IncreaseEffect = "Increases required hook height, requiring longer booms.",
            DecreaseEffect = "Lowers required hook height, allowing shorter booms.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "1.0 – 5.0",
            PerformanceImpact = "None",
            QualityImpact = "High — impacts boom clearance calculations."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "AdditionalWt",
            DisplayName = "Hook Weight (t)",
            Description = "Rigging accessory and hook block weight added to every element load before capacity resolution.",
            IncreaseEffect = "Decreases available net lifting capacity of the crane.",
            DecreaseEffect = "Allows lifting closer to rated capacity.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "0.0 – 5.0",
            PerformanceImpact = "None",
            QualityImpact = "High — accessory weights consume operating capacity."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "DeratingRatio",
            DisplayName = "Derating",
            Description = "Capacity derating multiplier applied to crane load chart values. Values are limited between 0 and 1. Entries outside this range are automatically clamped.",
            IncreaseEffect = "Increases effective capacity toward nominal chart levels.",
            DecreaseEffect = "Reduces capacity to compensate for wind, dynamic forces, or equipment age.",
            ValidRange = "0.0 to 1.0 (clamped automatically)",
            RecommendedRange = "0.85 – 0.95",
            PerformanceImpact = "None",
            QualityImpact = "High — scales all allowable load values from the load chart."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "SafetyFactor",
            DisplayName = "Safety Factor",
            Description = "Additional capacity safety multiplier applied after derating. Values are limited between 0 and 1. Entries outside this range are automatically clamped.",
            IncreaseEffect = "Allows lifting closer to the derated capacity limit.",
            DecreaseEffect = "Adds an extra safety margin by enforcing a stricter capacity threshold.",
            ValidRange = "0.0 to 1.0 (clamped automatically)",
            RecommendedRange = "0.90 – 0.95",
            PerformanceImpact = "None",
            QualityImpact = "High — critical engineering threshold applied after derating."
        });

        // 5. Advanced GA
        Add(new ParameterHelpMetadata
        {
            ParameterKey = "RandomSeed",
            DisplayName = "Random Seed",
            Description = "Seed value for random number generator. Set blank or null for non-deterministic behavior.",
            IncreaseEffect = "Forces repeatable, deterministic solver runs for testing.",
            DecreaseEffect = "Runs randomized exploration.",
            ValidRange = "Integer or blank",
            RecommendedRange = "42 (or null for production runs)",
            PerformanceImpact = "None",
            QualityImpact = "Low"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "CrossoverRate",
            DisplayName = "Crossover Rate",
            Description = "Probability of uniform coordinate crossover between parent solutions.",
            IncreaseEffect = "Increases recombination rate, mixing layout traits.",
            DecreaseEffect = "Preserves parent structures, relying more on mutation.",
            ValidRange = "0.0 to 1.0",
            RecommendedRange = "0.60 – 0.90",
            PerformanceImpact = "Low",
            QualityImpact = "Medium"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "MutationExplorationMultiplier",
            DisplayName = "Mutation Explore Mult",
            Description = "Scale factor applied to mutation perturbation step to prevent premature local clustering.",
            IncreaseEffect = "Expands mutation search area, promoting exploration.",
            DecreaseEffect = "Keeps mutation focused closely around parent coordinates.",
            ValidRange = "0.1 or greater",
            RecommendedRange = "1.5 – 3.0",
            PerformanceImpact = "Low",
            QualityImpact = "Medium"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "EnableAdaptiveSigma",
            DisplayName = "Enable Adaptive Sigma",
            Description = "Enables generation-dependent cooling schedule for mutation spread.",
            IncreaseEffect = "Gradually cools/shrinks mutation step size to lock in coordinate refinements.",
            DecreaseEffect = "Maintains constant mutation step size throughout the run.",
            ValidRange = "True/False",
            RecommendedRange = "False",
            PerformanceImpact = "Low",
            QualityImpact = "Medium"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "SigmaCoolingExponent",
            DisplayName = "Sigma Cooling Exponent",
            Description = "Controls decay shape of mutation sigma. Higher values decay faster.",
            IncreaseEffect = "Fast decay of mutation spread, shifting to exploitation early.",
            DecreaseEffect = "Slow decay of mutation spread, maintaining exploration longer.",
            ValidRange = "0.1 or greater",
            RecommendedRange = "1.0 – 2.0",
            PerformanceImpact = "Low",
            QualityImpact = "Medium"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "MinimumMutationSigma",
            DisplayName = "Min Mutation Sigma (m)",
            Description = "The floor for mutation sigma to prevent search freeze during adaptive cooling.",
            IncreaseEffect = "Maintains minimum exploration step size.",
            DecreaseEffect = "Allows mutation step size to approach zero.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "0.1 – 1.0",
            PerformanceImpact = "Low",
            QualityImpact = "Medium"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "PositionSamplingAttempts",
            DisplayName = "Sampling Attempts",
            Description = "Number of sample attempts used when generating valid and separated setup points in zones.",
            IncreaseEffect = "Improves solution validity during seeding, reducing failed chromosomes.",
            DecreaseEffect = "Speeds up initial population generation and mutation cycles.",
            ValidRange = "1 or greater",
            RecommendedRange = "50 – 150",
            PerformanceImpact = "Medium — scales setup coordinate sampling time.",
            QualityImpact = "Medium — critical for complex site shapes."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "EnableConcurrentOptimization",
            DisplayName = "Concurrent Opt",
            Description = "Enables multi-setup parallel solving. If false, runs greedy set-cover iterations sequentially.",
            IncreaseEffect = "Solves for all positions concurrently using complete multi-setup chromosomes.",
            DecreaseEffect = "Solves positions step-by-step using greedy iteration loops.",
            ValidRange = "True/False",
            RecommendedRange = "True",
            PerformanceImpact = "High — concurrent mode is computationally complex but runs GA once; greedy runs GA multiple times.",
            QualityImpact = "High — concurrent mode resolves global layout interactions better."
        });

        // 6. Diagnostics
        Add(new ParameterHelpMetadata
        {
            ParameterKey = "SlewOffsetX",
            DisplayName = "Slew Offset X (m)",
            Description = "Horizontal displacement of the crane slewing ring relative to base coordinates along X-axis.",
            IncreaseEffect = "Shifts the center of crane swing coordinates.",
            DecreaseEffect = "Centers crane swing coordinates.",
            ValidRange = "Any double",
            RecommendedRange = "0.0",
            PerformanceImpact = "None",
            QualityImpact = "Low"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "SlewOffsetY",
            DisplayName = "Slew Offset Y (m)",
            Description = "Horizontal displacement of the crane slewing ring relative to base coordinates along Y-axis.",
            IncreaseEffect = "Shifts the center of crane swing coordinates.",
            DecreaseEffect = "Centers crane swing coordinates.",
            ValidRange = "Any double",
            RecommendedRange = "0.0",
            PerformanceImpact = "None",
            QualityImpact = "Low"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "SlewOffsetZ",
            DisplayName = "Slew Offset Z (m)",
            Description = "Vertical displacement of the crane slewing ring relative to base coordinates along Z-axis.",
            IncreaseEffect = "Elevates crane swing center.",
            DecreaseEffect = "Lowers crane swing center.",
            ValidRange = "Any double",
            RecommendedRange = "0.0",
            PerformanceImpact = "None",
            QualityImpact = "Low"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "BoomHingeOffsetX",
            DisplayName = "Boom Hinge Offset X (m)",
            Description = "Horizontal displacement of the boom foot hinge relative to crane base coordinates.",
            IncreaseEffect = "Shifts boom hinge offset.",
            DecreaseEffect = "Centers boom hinge offset.",
            ValidRange = "Any double",
            RecommendedRange = "0.0",
            PerformanceImpact = "None",
            QualityImpact = "Low"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "BoomHingeOffsetY",
            DisplayName = "Boom Hinge Offset Y (m)",
            Description = "Horizontal displacement of the boom foot hinge relative to crane base coordinates.",
            IncreaseEffect = "Shifts boom hinge offset.",
            DecreaseEffect = "Centers boom hinge offset.",
            ValidRange = "Any double",
            RecommendedRange = "0.0",
            PerformanceImpact = "None",
            QualityImpact = "Low"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "BoomHingeOffsetZ",
            DisplayName = "Boom Hinge Offset Z (m)",
            Description = "Vertical displacement of the boom foot hinge relative to crane base coordinates.",
            IncreaseEffect = "Raises boom hinge offset.",
            DecreaseEffect = "Lowers boom hinge offset.",
            ValidRange = "Any double",
            RecommendedRange = "0.0",
            PerformanceImpact = "None",
            QualityImpact = "Low"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "MaxRadiusForNormalisation",
            DisplayName = "Max Radius Norm (m)",
            Description = "Distance normalization factor used to scale operating radius calculations into [0, 1] bounds.",
            IncreaseEffect = "Shrinks normalized radius values.",
            DecreaseEffect = "Expands normalized radius values.",
            ValidRange = "Greater than 0",
            RecommendedRange = "20.0 – 100.0",
            PerformanceImpact = "None",
            QualityImpact = "Medium — adjusts fitness formula scaling.",
            SpecialNote = "Must match or exceed the maximum reach of the selected crane specs."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "DuplicatePositionDistance",
            DisplayName = "Duplicate Dist (m)",
            Description = "Threshold distance below which final solution setup positions are merged as duplicates.",
            IncreaseEffect = "De-duplicates setup locations more aggressively.",
            DecreaseEffect = "Allows setup locations to be close together in final output.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "1.0 – 5.0",
            PerformanceImpact = "Low",
            QualityImpact = "Medium"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "CraneFootprintRadius",
            DisplayName = "Footprint Radius (m)",
            Description = "Footprint radius of outriggers/body used for obstacle collision and allowed zone checks.",
            IncreaseEffect = "Restricts placement coordinates, requiring larger clear areas.",
            DecreaseEffect = "Allows placement in tighter spaces.",
            ValidRange = "0.0 or greater",
            RecommendedRange = "Enforced from crane catalog dimensions",
            PerformanceImpact = "Low",
            QualityImpact = "High — ensures physical footprint clearance.",
            SpecialNote = "This setting is dynamically mapped from crane outrigger specs."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "EnableProfiling",
            DisplayName = "Enable Profiling",
            Description = "Enables high-resolution performance tracing and GC metric capture for the GA run.",
            IncreaseEffect = "Saves detailed profiling logs to disk for debugging.",
            DecreaseEffect = "Disables logging, saving slight disk write overhead.",
            ValidRange = "True/False",
            RecommendedRange = "True",
            PerformanceImpact = "Low",
            QualityImpact = "None"
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "ProfilingLogDirectory",
            DisplayName = "Profiling Log Directory",
            Description = "Target directory path where profiling metrics are written.",
            IncreaseEffect = "Writes to a custom folder location.",
            DecreaseEffect = "Falls back to default LocalAppData/Logs/GA/ directory.",
            ValidRange = "Valid directory path or blank",
            RecommendedRange = "Blank (autodetected relative to project folder)",
            PerformanceImpact = "Low",
            QualityImpact = "None"
        });

        // 7. Multi-Stage Discovery Parameters
        Add(new ParameterHelpMetadata
        {
            ParameterKey = "EnableMultiStageDiscovery",
            DisplayName = "Multi-Stage Discovery",
            Description = "Always enabled for normal optimization. The optimizer evaluates stages from 1 up to Max Crane Positions, returning the best result. Early stopping occurs when the coverage target is reached.",
            IncreaseEffect = "N/A — always enabled in the optimizer dialog.",
            DecreaseEffect = "N/A — always enabled in the optimizer dialog.",
            ValidRange = "Always True (normal UI runs)",
            RecommendedRange = "True",
            PerformanceImpact = "Medium — runs multiple GA stages sequentially.",
            QualityImpact = "High — finds the optimal number of crane setups rather than a fixed count.",
            SpecialNote = "This setting is locked to True in the normal UI. Developer and experiment paths can override it directly in GASettings."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "MultiStageCoverageTarget",
            DisplayName = "Coverage Target",
            Description = "Coverage ratio threshold at which multi-stage escalation stops early. When a stage achieves this ratio, no further stages are tested.",
            IncreaseEffect = "Requires higher coverage before stopping, potentially running more stages.",
            DecreaseEffect = "Stops escalation earlier, accepting lower coverage with fewer positions.",
            ValidRange = "0.0 to 1.0",
            RecommendedRange = "0.90 – 0.98",
            PerformanceImpact = "Medium — controls number of stages executed.",
            QualityImpact = "High — determines acceptable solution quality threshold."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "AdaptiveRelocationScaleFactor",
            DisplayName = "Adaptive Reloc. Scale",
            Description = "Scaling factor used in the adaptive relocation weight formula for stages 2 and above. Controls how aggressively the penalty grows as coverage increases.",
            IncreaseEffect = "Increases the adaptive relocation penalty in later stages.",
            DecreaseEffect = "Reduces the adaptive relocation penalty, allowing more setups more freely.",
            ValidRange = "0.0 to 1.0",
            RecommendedRange = "0.30 – 0.50",
            PerformanceImpact = "None",
            QualityImpact = "Medium — affects how aggressively relocation is penalized in multi-stage runs."
        });

        Add(new ParameterHelpMetadata
        {
            ParameterKey = "UseHungarianAssignment",
            DisplayName = "Hungarian Assignment",
            Description = "Enables global bipartite matching (Hungarian algorithm) for element-to-crane assignment instead of greedy nearest-crane assignment.",
            IncreaseEffect = "Produces globally optimal element assignments, potentially improving coverage.",
            DecreaseEffect = "Uses faster greedy assignment.",
            ValidRange = "True/False",
            RecommendedRange = "False",
            PerformanceImpact = "Medium — Hungarian algorithm is O(n³) but runs once per evaluation.",
            QualityImpact = "Medium — can improve assignment fairness on complex layouts."
        });
    }

    private static void Add(ParameterHelpMetadata metadata)
    {
        _registry[metadata.ParameterKey] = metadata;
    }

    public static bool TryGet(string key, out ParameterHelpMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            metadata = null!;
            return false;
        }
        return _registry.TryGetValue(key, out metadata!);
    }
}
