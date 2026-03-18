using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using WPF_IFC_test1.Models;

namespace WPF_IFC_test1.Analysis
{
    /// <summary>
    /// Tree-view node for the element selection hierarchy inside CraneOptimizerDialog.
    /// Supports tri-state checkbox propagation:
    ///   Checked      → all descendants are Checked
    ///   Unchecked    → all descendants are Unchecked
    ///   Indeterminate → children are mixed
    /// </summary>
    public class ElementHierarchyNode : INotifyPropertyChanged
    {
        // ── Internal state ─────────────────────────────────────────────────
        private bool? _isChecked = true;
        private bool  _suppressPropagation;

        // ── Identity ────────────────────────────────────────────────────────
        public string Label    { get; }
        public string SubLabel { get; }   // e.g. weight summary on leaf nodes

        /// <summary>Non-null only on leaf nodes.</summary>
        public IfcElementModel Element { get; }

        public bool IsLeaf   => Element != null;
        public bool IsParent => !IsLeaf;

        // ── Tree structure ──────────────────────────────────────────────────
        public ElementHierarchyNode Parent { get; private set; }
        public ObservableCollection<ElementHierarchyNode> Children { get; } = new();

        // ── Checkbox state ──────────────────────────────────────────────────

        /// <summary>
        /// Null = Indeterminate.  Setting to true/false propagates to all descendants.
        /// </summary>
        public bool? IsChecked
        {
            get => _isChecked;
            set => SetIsChecked(value, propagateDown: true, propagateUp: true);
        }

        private void SetIsChecked(bool? value, bool propagateDown, bool propagateUp)
        {
            if (_isChecked == value) return;
            _isChecked = value;
            OnPropertyChanged(nameof(IsChecked));

            if (propagateDown && value.HasValue)
            {
                foreach (var child in Children)
                {
                    child._suppressPropagation = true;
                    child.SetIsChecked(value, propagateDown: true, propagateUp: false);
                    child._suppressPropagation = false;
                }
            }

            if (propagateUp && !_suppressPropagation && Parent != null)
                Parent.RecalculateFromChildren();
        }

        private void RecalculateFromChildren()
        {
            if (Children.Count == 0) return;
            bool allChecked   = Children.All(c => c._isChecked == true);
            bool allUnchecked = Children.All(c => c._isChecked == false);
            bool? newVal = allChecked ? true : allUnchecked ? false : null;
            if (_isChecked != newVal)
            {
                _isChecked = newVal;
                OnPropertyChanged(nameof(IsChecked));
                Parent?.RecalculateFromChildren();
            }
        }

        // ── Constructors ────────────────────────────────────────────────────

        /// <summary>Parent (category) node.</summary>
        public ElementHierarchyNode(string label, string subLabel = "")
        {
            Label    = label;
            SubLabel = subLabel;
        }

        /// <summary>Leaf node wrapping a single element.</summary>
        public ElementHierarchyNode(IfcElementModel element)
        {
            Element  = element;
            Label    = element.DisplayName ?? element.Name ?? "–";
            double wt = (element.Weight ?? 0) / 1000.0;
            SubLabel = wt > 0 ? $"{wt:F2} t" : "no weight";
        }

        // ── Tree building helper ─────────────────────────────────────────────

        public void AddChild(ElementHierarchyNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        // ── Flat iteration ──────────────────────────────────────────────────

        /// <summary>Returns all leaf nodes that are currently Checked.</summary>
        public System.Collections.Generic.IEnumerable<IfcElementModel> GetCheckedElements()
        {
            if (IsLeaf)
            {
                if (_isChecked == true) yield return Element;
                yield break;
            }
            foreach (var child in Children)
                foreach (var el in child.GetCheckedElements())
                    yield return el;
        }

        // ── INotifyPropertyChanged ──────────────────────────────────────────
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
