# Genetic Algorithm Pipeline

The GA optimizes crane placement positions.

---

## Step 1 — Initialization

GAInitializer creates a random population.

Input:

- Crane placement zones
- Population size
- Max crane positions

Output:

Population of CraneSolution.

---

## Step 2 — Assignment Evaluation

ElementAssignmentService evaluates lifting feasibility.

Input:

- crane positions
- user element assignment
- crane specification
- IFC elements

Output:

AssignmentResult

---

## Step 3 — Fitness Evaluation

FitnessEvaluator calculates:

Coverage
RelocationPenalty
RadiusPenalty
OverweightPenalty
HeightPenalty

Output:

FitnessBreakdown

---

## Step 4 — Selection

TournamentSelection chooses parent solutions.

---

## Step 5 — Crossover

CrossoverOperator combines parent chromosomes.

---

## Step 6 — Mutation

MutationOperator modifies crane positions slightly.

---

## Step 7 — Replacement

ReplacementStrategy builds the next generation.

---

## Step 8 — Best Solution

GAEngine returns the best CraneSolution.