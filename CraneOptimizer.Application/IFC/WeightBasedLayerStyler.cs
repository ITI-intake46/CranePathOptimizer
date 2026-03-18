//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Windows.Media;
//using System.Windows.Media.Media3D;
//using WPF_IFC_test1.Models;
//using Xbim.Common;
//using Xbim.Common.Federation;
//using Xbim.Common.Geometry;
//using Xbim.Ifc;
//using Xbim.Ifc4.Interfaces;
//using Xbim.ModelGeometry.Scene;
//using Xbim.Presentation;
//using Xbim.Presentation.LayerStyling;

//namespace WPF_IFC_test1.Utilities
//{
//    /// <summary>
//    /// Custom layer styler that colors 3D elements based on their weight category.
//    /// Red (#E74C3C)    → > 5000 kg
//    /// Orange (#F39C12)  → > 3000 kg  
//    /// Gray              → normal elements
//    /// </summary>
//    public class WeightBasedLayerStyler : ILayerStyler
//    {
//        private readonly Dictionary<int, double> _weightByEntityLabel;
//        private readonly XbimColour _heavyColor;
//        private readonly XbimColour _mediumColor;
//        private readonly XbimColour _normalColor;

//        public WeightBasedLayerStyler(IEnumerable<IfcElementModel> elements, IModel model)
//        {
//            _weightByEntityLabel = new Dictionary<int, double>();

//            if (elements != null && model != null)
//            {
//                var weightByGlobalId = new Dictionary<string, double>();
//                foreach (var el in elements)
//                {
//                    if (!string.IsNullOrEmpty(el.GlobalId) && el.Weight.HasValue && el.Weight.Value > 0)
//                        weightByGlobalId[el.GlobalId] = el.Weight.Value;
//                }

//                foreach (var product in model.Instances.OfType<IIfcProduct>())
//                {
//                    if (weightByGlobalId.TryGetValue(product.GlobalId, out double weight))
//                        _weightByEntityLabel[product.EntityLabel] = weight;
//                }
//            }

//            _heavyColor = new XbimColour("Heavy", 0.906f, 0.298f, 0.235f, 0.9f);
//            _mediumColor = new XbimColour("Medium", 0.953f, 0.612f, 0.071f, 0.9f);
//            _normalColor = new XbimColour("Normal", 0.65f, 0.65f, 0.68f, 0.5f);

//            Debug.WriteLine($"\n=== WeightBasedLayerStyler Created ===");
//            Debug.WriteLine($"  Elements with weight: {_weightByEntityLabel.Count}");
//            Debug.WriteLine($"  Heavy (>5000kg): {_weightByEntityLabel.Count(kv => kv.Value > 5000)}");
//            Debug.WriteLine($"  Medium (>3000kg): {_weightByEntityLabel.Count(kv => kv.Value > 3000 && kv.Value <= 5000)}");
//            Debug.WriteLine($"=====================================\n");
//        }

//        private XbimColour GetColorForEntity(int entityLabel)
//        {
//            if (_weightByEntityLabel.TryGetValue(entityLabel, out double weight))
//            {
//                if (weight > 5000) return _heavyColor;
//                if (weight > 3000) return _mediumColor;
//            }
//            return _normalColor;
//        }

//        /// <summary>
//        /// Builds the 3D scene with weight-based colors.
//        /// This is the ONLY BuildScene signature that Xbim's ILayerStyler requires.
//        /// </summary>
//        public XbimScene<WpfMeshGeometry3D, WpfMaterial> BuildScene(
//            IModel model,
//            XbimMatrix3D modelTransform,
//            ModelVisual3D opaqueShapes,
//            ModelVisual3D transparentShapes,
//            List<IPersistEntity> isolateInstances = null,
//            List<IPersistEntity> hideInstances = null,
//            List<Type> excludeTypes = null)
//        {
//            var scene = new XbimScene<WpfMeshGeometry3D, WpfMaterial>(model);

//            if (model == null)
//                return scene;

//            try
//            {
//                var context = new Xbim3DModelContext(model);
//                context.CreateContext();

//                // Build filter sets
//                var hideLabels = new HashSet<int>();
//                if (hideInstances != null)
//                    foreach (var e in hideInstances)
//                        hideLabels.Add(e.EntityLabel);

//                HashSet<int> isolateLabels = null;
//                if (isolateInstances != null && isolateInstances.Any())
//                {
//                    isolateLabels = new HashSet<int>();
//                    foreach (var e in isolateInstances)
//                        isolateLabels.Add(e.EntityLabel);
//                }

//                // Get all visible shape instances
//                var allShapes = context.ShapeInstances()
//                    .Where(s => s.RepresentationType == XbimGeometryRepresentationType.OpeningsAndAdditionsIncluded);

//                if (hideLabels.Count > 0)
//                    allShapes = allShapes.Where(s => !hideLabels.Contains(s.IfcProductLabel));
//                if (isolateLabels != null)
//                    allShapes = allShapes.Where(s => isolateLabels.Contains(s.IfcProductLabel));

//                var shapeList = allShapes.ToList();
//                Debug.WriteLine($"WeightBasedLayerStyler: {shapeList.Count} shape instances");

//                // Group shapes by weight category
//                var groups = new Dictionary<string, List<XbimShapeInstance>>
//                {
//                    ["Heavy"] = new List<XbimShapeInstance>(),
//                    ["Medium"] = new List<XbimShapeInstance>(),
//                    ["Normal"] = new List<XbimShapeInstance>()
//                };

//                foreach (var shape in shapeList)
//                {
//                    var col = GetColorForEntity(shape.IfcProductLabel);
//                    groups[col.Name].Add(shape);
//                }

//                // Build a WPF visual for each colour group
//                foreach (var kvp in groups)
//                {
//                    var shapes = kvp.Value;
//                    if (shapes.Count == 0) continue;

//                    var colour = kvp.Key == "Heavy" ? _heavyColor
//                               : kvp.Key == "Medium" ? _mediumColor
//                               : _normalColor;

//                    var wpfMat = new WpfMaterial();
//                    wpfMat.CreateMaterial(colour);

//                    var meshBuilder = new WpfMeshGeometry3D(wpfMat, wpfMat);
//                    meshBuilder.BeginUpdate();

//                    foreach (var shape in shapes)
//                    {
//                        try
//                        {
//                            IXbimShapeGeometryData geom = context.ShapeGeometry(shape);
//                            if (geom.Format != (byte)XbimGeometryType.PolyhedronBinary)
//                                continue;

//                            // ✅ FIX 1: shape.Transformation is already XbimMatrix3D.
//                            //           Just multiply with the model-level transform.
//                            var xform = XbimMatrix3D.Multiply(shape.Transformation, modelTransform);

//                            meshBuilder.Add(
//                                geom.ShapeData,
//                                shape.IfcTypeId,
//                                shape.IfcProductLabel,
//                                shape.InstanceLabel,
//                                xform,
//                                (short)model.UserDefinedId);
//                        }
//                        catch (Exception ex)
//                        {
//                            Debug.WriteLine($"  Skip shape {shape.InstanceLabel}: {ex.Message}");
//                        }
//                    }

//                    meshBuilder.EndUpdate();

//                    // ✅ FIX 2: Use Mesh.Positions.Count instead of TriangleCount
//                    if (meshBuilder.Mesh == null || meshBuilder.Mesh.Positions.Count == 0)
//                        continue;

//                    // Create WPF material from colour
//                    var brush = new SolidColorBrush(
//                        Color.FromArgb(
//                            (byte)(colour.Alpha * 255),
//                            (byte)(colour.Red * 255),
//                            (byte)(colour.Green * 255),
//                            (byte)(colour.Blue * 255)));
//                    brush.Freeze();

//                    var diffuse = new DiffuseMaterial(brush);
//                    diffuse.Freeze();

//                    // ✅ FIX 3: Build standard WPF GeometryModel3D → ModelVisual3D
//                    //           and add to the viewer's visual containers directly,
//                    //           instead of calling scene.Add(mesh).
//                    var geoModel = new GeometryModel3D(meshBuilder.Mesh, diffuse);
//                    geoModel.BackMaterial = diffuse;
//                    geoModel.Freeze();

//                    var modelGroup = new Model3DGroup();
//                    modelGroup.Children.Add(geoModel);
//                    modelGroup.Freeze();

//                    var visual = new ModelVisual3D { Content = modelGroup };

//                    // Normal weight (alpha < 0.55) → transparent, otherwise → opaque
//                    if (colour.Alpha < 0.55f)
//                        transparentShapes.Children.Add(visual);
//                    else
//                        opaqueShapes.Children.Add(visual);

//                    Debug.WriteLine($"  ✓ {kvp.Key}: {shapes.Count} shapes, {meshBuilder.Mesh.Positions.Count} verts");
//                }

//                Debug.WriteLine("WeightBasedLayerStyler: scene built OK");
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine($"WeightBasedLayerStyler ERROR: {ex.Message}");
//                Debug.WriteLine(ex.StackTrace);

//                // ✅ FIX 4: Fallback with correct full signature
//                try
//                {
//                    Debug.WriteLine("Falling back to SurfaceLayerStyler...");
//                    var fallback = new SurfaceLayerStyler();
//                    return fallback.BuildScene(
//                        model, modelTransform,
//                        opaqueShapes, transparentShapes,
//                        isolateInstances, hideInstances, excludeTypes);
//                }
//                catch (Exception fbEx)
//                {
//                    Debug.WriteLine($"Fallback failed too: {fbEx.Message}");
//                }
//            }

//            return scene;
//        }

//        public void SetFederationEnvironment(IReferencedModel refModel)
//        {
//            // Not needed for single-model usage
//        }

//        public void Clear()
//        {
//            // Nothing to dispose
//        }
//    }
//}