# Optimization Engine - Multi-Stage GA Orchestrator

This document details the architecture, routing, and escalation logic of the multi-stage Genetic Algorithm optimization engine.

---

## 1. Overview

The [MultiStageGAOrchestrator](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/MultiStageGAOrchestrator.cs) implements the [IMultiStageGAOrchestrator](file:///F:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Optimization/GeneticAlgorithms/Interfaces/IMultiStageGAOrchestrator.cs) interface. It is responsible for orchestrating the escalation loop of the Genetic Algorithm to dynamically discover the optimal number of crane setup positions.

By scaling setup counts incrementally from 1 to 3, the orchestrator minimizes travel/relocation overhead while ensuring the project's target lifting coverage is achieved.

---

## 2. Escalation Workflow

```mermaid
graph TD
    Start([Start Orchestrator Run]) --> Stage1[Run Stage 1: MaxCranePositions = 1 <br/> RelocationWeight = 0]
    Stage1 --> CheckTarget1{Coverage >= Target?}
    CheckTarget1 -- Yes --> Terminate1[Terminate: CoverageTargetMet]
    CheckTarget1 -- No --> CheckMax1{MaxCranePositions == 1?}
    CheckMax1 -- Yes --> Terminate2[Terminate: MaxStagesReached]
    
    CheckMax1 -- No --> Stage2[Run Stage 2: MaxCranePositions = 2 <br/> Calculate Adaptive Relocation Weight]
    Stage2 --> CheckTarget2{Coverage >= Target?}
    CheckTarget2 -- Yes --> Terminate1
    CheckTarget2 -- No --> CheckMax2{MaxCranePositions == 2?}
    CheckMax2 -- Yes --> Terminate2
    
    CheckMax2 -- No --> Stage3[Run Stage 3: MaxCranePositions = 3 <br/> Calculate Adaptive Relocation Weight]
    Stage3 --> CheckTarget3{Coverage >= Target?}
    CheckTarget3 -- Yes --> Terminate1
    CheckTarget3 -- No --> Terminate2
```

---

## 3. Key Responsibilities

1. **Dynamic Setup Scaling:** 
   Increments `MaxCranePositions` on each stage to ensure single-crane solutions are prioritized and multi-position setups are only introduced when physically necessary.
2. **Adaptive Weight Scaling:** 
   Computes a reduced relocation penalty for subsequent stages to allow multi-position chromosomes to survive in the GA population.
3. **Early Early-Break:** 
   Monitors execution and breaks out immediately if the coverage target is met during any intermediate stage, conserving CPU cycles.
4. **Cancellation Propagation:** 
   Monitors the `CancellationToken` and gracefully returns the best results obtained in completed stages if interrupted mid-run.
