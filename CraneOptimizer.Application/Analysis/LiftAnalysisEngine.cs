using System;
using System.Collections.Generic;
using WPF_IFC_test1.Database.Entities;
using WPF_IFC_test1.Database.Services;
using WPF_IFC_test1.Models;
using WPF_IFC_test1.Utilities;

namespace WPF_IFC_test1.Analysis
{
    /// <summary>
    /// Stateless engine that runs the 4-step lift feasibility check
    /// for a single IFC element against a specific crane position and load chart.
    ///
    /// Step 1 – Horizontal radius check (MaxReach)
    /// Step 2 – Load chart capacity check
    /// Step 3 – Delivery height check (HookZ >= elementCG_Z + minClearance)
    /// Step 4 – Assign Liftable
    /// </summary>
    public static class LiftAnalysisEngine
    {
        /// <summary>
        /// Analyses one element and returns its lift result.
        /// </summary>
        /// <param name="element">IFC element with COG_X/Y/Z and Weight [kg].</param>
        /// <param name="craneX">Crane X world position for this candidate.</param>
        /// <param name="craneY">Crane Y world position for this candidate.</param>
        /// <param name="crane">Crane instance (parameters, type, etc.).</param>
        /// <param name="chartPoints">All load-chart data points for the assigned chart.</param>
        /// <param name="minClearanceM">Minimum hook-to-element vertical clearance (default 0.5 m).</param>
        public static ElementLiftResult Analyze(
            IfcElementModel                  element,
            double                           craneX,
            double                           craneY,
            CraneInstance                    crane,
            List<CraneLoadChartPointEntity>  chartPoints,
            double                           minClearanceM = 0.5)
        {
            double cogX = element.COG_X ?? 0;
            double cogY = element.COG_Y ?? 0;
            double cogZ = element.COG_Z ?? 0;

            // ── Step 1: Horizontal radius (XY plane only — load charts use XY) ──
            double radius = Math.Sqrt(
                Math.Pow(cogX - craneX, 2) +
                Math.Pow(cogY - craneY, 2));

            if (radius > crane.Parameters.MaxReach)
                return new ElementLiftResult(element, LiftStatus.Unreachable, radius);

            // ── Step 2: Load chart capacity ──────────────────────────────────────
            // boom length = MaxReach (both TowerCraneParameters.MaxReach = BoomLength
            //                       and MobileCraneParameters.MaxReach = BoomLength)
            double boomLength = crane.Parameters.MaxReach;

            double? capacity = (chartPoints != null && chartPoints.Count > 0)
                ? CraneLoadChartLookup.GetCapacity(chartPoints, radius, boomLength)
                : null;

            if (capacity == null)
                return new ElementLiftResult(element, LiftStatus.Unreachable, radius);

            double weightTonnes = (element.Weight ?? 0) / 1000.0;
            if (weightTonnes > capacity.Value)
                return new ElementLiftResult(element, LiftStatus.NonLiftable, radius, capacity);

            // ── Step 3: Delivery height ──────────────────────────────────────────
            // HookHeight is relative to the crane base (PositionZ).
            double hookZ = crane.Parameters.HookHeight + crane.Parameters.PositionZ;
            if (hookZ < cogZ + minClearanceM)
                return new ElementLiftResult(element, LiftStatus.NonLiftable, radius, capacity);

            // ── Step 4: All checks passed ────────────────────────────────────────
            return new ElementLiftResult(element, LiftStatus.Liftable, radius, capacity);
        }

        /// <summary>
        /// Runs the analysis for every element in <paramref name="elements"/> at a fixed crane position.
        /// </summary>
        public static List<ElementLiftResult> AnalyzeAll(
            IEnumerable<IfcElementModel>     elements,
            double                           craneX,
            double                           craneY,
            CraneInstance                    crane,
            List<CraneLoadChartPointEntity>  chartPoints,
            double                           minClearanceM = 0.5)
        {
            var results = new List<ElementLiftResult>();
            foreach (var el in elements)
                results.Add(Analyze(el, craneX, craneY, crane, chartPoints, minClearanceM));
            return results;
        }
    }
}
