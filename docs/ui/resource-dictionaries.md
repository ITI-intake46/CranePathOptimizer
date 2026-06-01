# UI Resource Dictionaries

WPF styling is organized into separate XAML resource dictionaries to modularize style definitions.

---

## 1. Directory Layout

All UI styling resources are located in [CraneOptimizer.UI/Resources/](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Resources):

```
Resources/
├── Colors.xaml                # Color system palette brushes
├── Fonts.xaml                 # Font family resource keys (Poppins mapping)
├── Styles.xaml                # Text, DataGrid, Card, TextBox control templates
├── ButtonStyles.xaml          # NavButton, Primary, Secondary, Danger, Icon button styles
├── Converters.xaml            # Value converters (BoolToVisibility, NullToVisibility)
└── ViewDataTemplates.xaml     # Navigation view model-to-view mapping rules
```

---

## 2. Merged Dictionaries configuration

These resource dictionaries are loaded during startup in the `App.xaml` root dictionary:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Resources/Colors.xaml" />
            <ResourceDictionary Source="Resources/Fonts.xaml" />
            <ResourceDictionary Source="Resources/Styles.xaml" />
            <ResourceDictionary Source="Resources/ButtonStyles.xaml" />
            <ResourceDictionary Source="Resources/Converters.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

> [!NOTE]
> The order of dictionaries inside `MergedDictionaries` matters. Dictionaries that depend on other dictionaries (like `Styles.xaml` depending on brushes defined in `Colors.xaml` and fonts in `Fonts.xaml`) must be loaded *after* their dependencies.

---

## 3. Resolving Dynamic vs. Static Resources

* **Brushes (`Colors.xaml`):** Referenced using `StaticResource` when a style or layout has a fixed value (e.g. `Background="{StaticResource BackgroundDark}"`).
* **Fonts (`Fonts.xaml`):** Standardized font family bindings (e.g. `FontFamily="{StaticResource PoppinsRegular}"`).
* **Button Styles:** Applied to buttons using the `Style` key (e.g. `Style="{StaticResource PrimaryButton}"`).
