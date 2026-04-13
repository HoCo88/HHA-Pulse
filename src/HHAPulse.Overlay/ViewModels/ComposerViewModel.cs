using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using HHAPulse.Shared;

namespace HHAPulse.Overlay.ViewModels;

/// <summary>
/// State for the 4-step Custom composer: layout pick, module toggles,
/// per-module detail level, and reordered metric list. Simple INPC; no
/// persistence, no service references.
/// </summary>
public sealed class ComposerViewModel : ViewModelBase
{
    private ComposerLayout selectedLayout = ComposerLayout.TopBar;
    private int currentStep = 1;
    private bool showGraph;
    private bool showGraphManuallyOverridden;

    public ComposerViewModel()
    {
        SelectedModuleIds.CollectionChanged += OnSelectedModulesChanged;
        OrderedMetricIds.CollectionChanged += (_, _) => OnPropertyChanged(nameof(OrderedMetricIds));
    }

    public IReadOnlyList<ModuleDetailPreset> AllModules => ComposerModuleCatalog.All;

    public ObservableCollection<string> SelectedModuleIds { get; } = new();

    public Dictionary<string, ComposerDetail> ModuleDetail { get; } = new();

    public ObservableCollection<string> OrderedMetricIds { get; } = new();

    public ComposerLayout SelectedLayout
    {
        get => selectedLayout;
        set => SetProperty(ref selectedLayout, value);
    }

    public int CurrentStep
    {
        get => currentStep;
        set
        {
            var clamped = value < 1 ? 1 : value > 4 ? 4 : value;
            SetProperty(ref currentStep, clamped);
        }
    }

    public bool ShowGraph
    {
        get => showGraph;
        set
        {
            showGraphManuallyOverridden = true;
            SetProperty(ref showGraph, value);
        }
    }

    public bool IsGraphSuggested { get; private set; }

    public bool CanGoNext => CurrentStep < 4;
    public bool CanGoBack => CurrentStep > 1;

    public void ToggleModule(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
        {
            return;
        }

        if (SelectedModuleIds.Contains(moduleId))
        {
            SelectedModuleIds.Remove(moduleId);
            ModuleDetail.Remove(moduleId);
        }
        else
        {
            SelectedModuleIds.Add(moduleId);
            ModuleDetail[moduleId] = ComposerDetail.Summary;
        }

        RebuildOrderedMetricIds();
    }

    public bool IsModuleSelected(string moduleId) => SelectedModuleIds.Contains(moduleId);

    public ComposerDetail GetDetail(string moduleId)
    {
        return ModuleDetail.TryGetValue(moduleId, out var detail) ? detail : ComposerDetail.Off;
    }

    public void SetDetail(string moduleId, ComposerDetail detail)
    {
        if (string.IsNullOrEmpty(moduleId))
        {
            return;
        }

        var preset = ComposerModuleCatalog.Find(moduleId);
        if (preset is null)
        {
            return;
        }

        if (detail == ComposerDetail.Tuning && !preset.SupportsTuning)
        {
            detail = ComposerDetail.Summary;
        }

        if (detail == ComposerDetail.Deep && !preset.SupportsDeep)
        {
            detail = preset.SupportsTuning ? ComposerDetail.Tuning : ComposerDetail.Summary;
        }

        if (detail == ComposerDetail.Off)
        {
            if (SelectedModuleIds.Contains(moduleId))
            {
                SelectedModuleIds.Remove(moduleId);
            }

            ModuleDetail.Remove(moduleId);
        }
        else
        {
            if (!SelectedModuleIds.Contains(moduleId))
            {
                SelectedModuleIds.Add(moduleId);
            }

            ModuleDetail[moduleId] = detail;
        }

        OnPropertyChanged(nameof(ModuleDetail));
        RebuildOrderedMetricIds();
    }

    public void MoveMetric(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= OrderedMetricIds.Count)
        {
            return;
        }

        if (toIndex < 0)
        {
            toIndex = 0;
        }

        if (toIndex >= OrderedMetricIds.Count)
        {
            toIndex = OrderedMetricIds.Count - 1;
        }

        if (fromIndex == toIndex)
        {
            return;
        }

        OrderedMetricIds.Move(fromIndex, toIndex);
    }

    public void Reset()
    {
        SelectedModuleIds.Clear();
        ModuleDetail.Clear();
        OrderedMetricIds.Clear();
        SelectedLayout = ComposerLayout.TopBar;
        CurrentStep = 1;
        showGraphManuallyOverridden = false;
        SetProperty(ref showGraph, false, nameof(ShowGraph));
        UpdateGraphSuggestion();
    }

    public CustomPresetDraft CreateDraft()
    {
        var detailCopy = new Dictionary<string, ComposerDetail>(ModuleDetail);
        var orderedCopy = OrderedMetricIds.ToArray();
        return new CustomPresetDraft(SelectedLayout, orderedCopy, detailCopy, ShowGraph);
    }

    private void OnSelectedModulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedModuleIds));
    }

    private void RebuildOrderedMetricIds()
    {
        var existing = OrderedMetricIds.ToList();
        var desired = new List<string>();
        foreach (var moduleId in SelectedModuleIds)
        {
            var preset = ComposerModuleCatalog.Find(moduleId);
            if (preset is null)
            {
                continue;
            }

            var detail = GetDetail(moduleId);
            foreach (var metricId in preset.GetMetricIdsForDetail(detail))
            {
                if (!desired.Contains(metricId))
                {
                    desired.Add(metricId);
                }
            }
        }

        // Preserve prior user ordering for metrics that still apply.
        var preserved = existing.Where(id => desired.Contains(id)).ToList();
        var added = desired.Where(id => !preserved.Contains(id)).ToList();
        var final = preserved.Concat(added).ToList();

        if (SequenceEquals(OrderedMetricIds, final))
        {
            UpdateGraphSuggestion();
            return;
        }

        OrderedMetricIds.Clear();
        foreach (var metric in final)
        {
            OrderedMetricIds.Add(metric);
        }

        UpdateGraphSuggestion();
    }

    private static bool SequenceEquals(IList<string> a, IList<string> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i], b[i], System.StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateGraphSuggestion()
    {
        var metrics = OrderedMetricIds;
        var hasFrametime = metrics.Contains("frametime");
        var fpsFamilyCount = metrics.Count(IsFpsFamily);
        var suggested = hasFrametime || fpsFamilyCount >= 3;

        if (suggested != IsGraphSuggested)
        {
            IsGraphSuggested = suggested;
            OnPropertyChanged(nameof(IsGraphSuggested));
        }

        if (!showGraphManuallyOverridden && suggested != showGraph)
        {
            SetProperty(ref showGraph, suggested, nameof(ShowGraph));
        }
    }

    private static bool IsFpsFamily(string metricId)
    {
        return metricId is "fps"
            or "avg_fps"
            or "one_percent_low"
            or "zero_point_one_low"
            or "frametime";
    }
}
