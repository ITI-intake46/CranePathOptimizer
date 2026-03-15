# Team Development Workflow

The GA development is split across 4 students.

Each student works in **one file only**.

---

## Student 1 – Selection

File:

Services/TournamentSelection.cs

Task:

Implement SelectParents()

Algorithm:

Tournament Selection

---

## Student 2 – Crossover

File:

Services/CrossoverOperator.cs

Task:

Combine parent chromosomes.

Suggested strategy:

Single Point Crossover.

---

## Student 3 – Mutation

File:

Services/MutationOperator.cs

Task:

Modify crane position slightly.

Example:

X += random small value
Y += random small value

---

## Student 4 – Replacement

File:

Services/ReplacementStrategy.cs

Task:

Create next generation.

Suggested strategy:

Elitism.

---

## Files that must NOT be modified

- GAEngine
- GAInitializer
- GASettings
- LiftCalculation
- CraneSolution
- CraneChromosome
- Population