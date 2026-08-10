using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F11_DXF.DTOs;
using CraneOptimizer.UI.Features.F11_DXF.Views;
using System.IO;

namespace CraneOptimizer.UI.Features.F11_DXF.ViewModels
{
    public sealed class DxfSourceSession : ViewModelBase
    {
        private bool _isSelected;
        private bool _isActive;
        private bool _useDxfColors;
        private bool _isAttached = true;
        private bool _hasSavedTransform;
        private double[] _savedTransformArray;

        public DxfSourceSession(DxfSourceState state)
        {
            Id = state.Id == Guid.Empty ? Guid.NewGuid() : state.Id;
            FilePath = state.FilePath;
            DisplayName = string.IsNullOrWhiteSpace(state.DisplayName)
                ? Path.GetFileName(state.FilePath)
                : state.DisplayName;
            LoadedOrder = state.LoadedOrder;
            _isActive = state.IsActive;
            _isSelected = state.IsSelected;
            _useDxfColors = state.UseDxfColors;
            _hasSavedTransform = state.HasSavedTransform;
            _savedTransformArray = (double[])state.TransformValues.Clone();
            SelectionTarget = new SceneDxf
            {
                Id = Id,
                Geometry = this,
                Layer = DisplayName,
                IsSelected = state.IsSelected
            };
        }

        public Guid Id { get; }
        public string FilePath { get; }
        public string DisplayName { get; }
        public int LoadedOrder { get; internal set; }
        public SceneDxf SelectionTarget { get; }
        public DxfUploader? Uploader { get; internal set; }
        internal CancellationTokenSource? LoadCancellation { get; set; }
        public bool HasSavedTransform
        {
            get => _hasSavedTransform;
            internal set
            {
                if (_hasSavedTransform == value) return;
                _hasSavedTransform = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AlignmentStatus));
                OnPropertyChanged(nameof(TransformSummary));
            }
        }
        public bool IsAttached
        {
            get => _isAttached;
            internal set
            {
                if (_isAttached == value) return;
                _isAttached = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            internal set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                SelectionTarget.IsSelected = value;
                Uploader?.SetSelectionHighlight(value);
                OnPropertyChanged();
            }
        }

        public bool IsActive
        {
            get => _isActive;
            internal set
            {
                if (_isActive == value) return;
                _isActive = value;
                OnPropertyChanged();
            }
        }

        public bool UseDxfColors
        {
            get => _useDxfColors;
            internal set
            {
                if (_useDxfColors == value) return;
                _useDxfColors = value;
                if (Uploader != null)
                    Uploader.UseDxfColors = value;
                OnPropertyChanged();
            }
        }

        public double[] SavedTransformArray
        {
            get => (double[])_savedTransformArray.Clone();
            internal set
            {
                _savedTransformArray = (double[])value.Clone();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TransformSummary));
                OnPropertyChanged(nameof(AlignmentStatus));
            }
        }

        public string AlignmentStatus => HasSavedTransform ? "Aligned" : "Not aligned";

        public string TransformSummary
        {
            get
            {
                if (_savedTransformArray.Length < 4)
                    return AlignmentStatus;

                return $"X {_savedTransformArray[0]:0.###}, Y {_savedTransformArray[1]:0.###}, " +
                       $"S {_savedTransformArray[2]:0.###}, R {_savedTransformArray[3]:0.###} deg";
            }
        }

        public DxfSourceState ToState() => new(
            Id,
            FilePath,
            SavedTransformArray,
            LoadedOrder,
            IsActive,
            IsSelected,
            HasSavedTransform,
            UseDxfColors,
            DisplayName);
    }
}
