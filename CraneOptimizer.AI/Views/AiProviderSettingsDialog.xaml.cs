using System.Windows;
using System.Windows.Controls;
using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.Interfaces;

using CraneOptimizer.AI.Runtime;
namespace CraneOptimizer.AI.Views
{
    public partial class AiProviderSettingsDialog : Window
    {
        private readonly IAiProviderManagementService _providerManagement;
        private readonly string _providerId;
        private AiProviderProfileSettings _profile;

        public AiProviderSettingsDialog(string providerId, IAiProviderManagementService providerManagement)
        {
            InitializeComponent();
            _providerId = providerId ?? throw new ArgumentNullException(nameof(providerId));
            _providerManagement = providerManagement ?? throw new ArgumentNullException(nameof(providerManagement));
            _profile = _providerManagement.GetProfile(providerId);
            LoadProfile();
        }

        public bool SettingsChanged { get; private set; }

        private void LoadProfile()
        {
            ProviderNameText.Text = _profile.DisplayName;
            BaseUrlTextBox.Text = _profile.BaseUrl;
            ChatPathTextBox.Text = _profile.ChatCompletionsPath;
            ModelsPathTextBox.Text = _profile.ModelsPath;
            DefaultModelTextBox.Text = _profile.DefaultModel;
            ApiKeyHeaderTextBox.Text = _profile.ApiKeyHeaderName;
            SelectAuthentication(_profile.AuthenticationMode);
            ModelDiscoveryCheckBox.IsChecked = _profile.SupportsModelDiscovery;
            StreamingCheckBox.IsChecked = _profile.SupportsStreaming;
            SendTemperatureCheckBox.IsChecked = _profile.SendTemperature;
            AppUrlTextBox.Text = _profile.AdditionalHeaders.TryGetValue("HTTP-Referer", out var appUrl) ? appUrl : string.Empty;
            AppTitleTextBox.Text = _profile.AdditionalHeaders.TryGetValue("X-OpenRouter-Title", out var title) ? title : string.Empty;
            AdditionalHeadersTextBox.Text = string.Join(Environment.NewLine, _profile.AdditionalHeaders
                .Where(header => !header.Key.Equals("HTTP-Referer", StringComparison.OrdinalIgnoreCase)
                                 && !header.Key.Equals("X-OpenRouter-Title", StringComparison.OrdinalIgnoreCase))
                .OrderBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
                .Select(header => $"{header.Key}={header.Value}"));

            var isLocal = AiEndpointSecurityClassifier.IsLocalEndpoint(_profile.BaseUrl);
            AuthenticationComboBox.IsEnabled = !isLocal;
            AppUrlTextBox.IsEnabled = !isLocal;
            AppTitleTextBox.IsEnabled = !isLocal;
            AdditionalHeadersTextBox.IsEnabled = !isLocal;
            SendTemperatureCheckBox.IsEnabled = !isLocal;
            UpdateAuthenticationUi();
            if (isLocal)
            {
                StatusText.Text = "Local Ollama does not require an API key. Configure its endpoint, then use Check in AI Studio.";
            }
            else
            {
                try
                {
                    StatusText.Text = _providerManagement.HasCredential(_providerId)
                        ? "A protected API key is stored. Leave the key field blank to keep it."
                        : "No API key is stored for this provider.";
                }
                catch (Exception ex)
                {
                    StatusText.Text = "Windows Credential Manager could not be inspected: " + ex.Message;
                }
            }
        }

        private void SelectAuthentication(string mode)
        {
            foreach (var item in AuthenticationComboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Content?.ToString(), mode, StringComparison.OrdinalIgnoreCase))
                {
                    AuthenticationComboBox.SelectedItem = item;
                    return;
                }
            }
            AuthenticationComboBox.SelectedIndex = 0;
        }


        private void AuthenticationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateAuthenticationUi();

        private void UpdateAuthenticationUi()
        {
            if (ApiKeyLabel == null || ApiKeyGrid == null || ApiKeyHeaderTextBox == null)
                return;

            var isLocal = AiEndpointSecurityClassifier.IsLocalEndpoint(_profile?.BaseUrl);
            var mode = (AuthenticationComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? _profile?.AuthenticationMode ?? "Bearer";
            var requiresCredential = !isLocal && !mode.Equals("None", StringComparison.OrdinalIgnoreCase);
            ApiKeyLabel.Visibility = requiresCredential ? Visibility.Visible : Visibility.Collapsed;
            ApiKeyGrid.Visibility = requiresCredential ? Visibility.Visible : Visibility.Collapsed;
            ApiKeyHeaderTextBox.IsEnabled = !isLocal && mode.Equals("ApiKeyHeader", StringComparison.OrdinalIgnoreCase);
        }

        private bool SaveProfile(bool closeDialog)
        {
            var baseUrl = BaseUrlTextBox.Text.Trim();
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint)
                || (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp))
            {
                MessageBox.Show(this, "Enter a valid HTTP or HTTPS base URL.", "AI Provider", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var authenticationMode = (AuthenticationComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Bearer";
            if (!AiEndpointSecurityClassifier.IsLocalEndpoint(endpoint)
                && endpoint.Scheme != Uri.UriSchemeHttps)
            {
                MessageBox.Show(this, "Remote AI provider endpoints must use HTTPS.", "AI Provider", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            _profile.BaseUrl = baseUrl;
            _profile.IsLocal = AiEndpointSecurityClassifier.IsLocalEndpoint(endpoint);
            _profile.ChatCompletionsPath = ChatPathTextBox.Text.Trim();
            _profile.ModelsPath = ModelsPathTextBox.Text.Trim();
            _profile.DefaultModel = DefaultModelTextBox.Text.Trim();
            _profile.AuthenticationMode = authenticationMode;
            _profile.ApiKeyHeaderName = ApiKeyHeaderTextBox.Text.Trim();
            _profile.SupportsModelDiscovery = ModelDiscoveryCheckBox.IsChecked == true;
            _profile.SupportsStreaming = StreamingCheckBox.IsChecked == true;
            _profile.SendTemperature = SendTemperatureCheckBox.IsChecked == true;
            if (!TryApplyAdditionalHeaders())
                return false;
            SetOptionalHeader("HTTP-Referer", AppUrlTextBox.Text);
            SetOptionalHeader("X-OpenRouter-Title", AppTitleTextBox.Text);

            try
            {
                _providerManagement.UpdateProfile(_profile);
                if (!string.IsNullOrWhiteSpace(ApiKeyPasswordBox.Password))
                {
                    _providerManagement.SaveCredential(_providerId, ApiKeyPasswordBox.Password);
                    ApiKeyPasswordBox.Clear();
                }

                _profile = _providerManagement.GetProfile(_providerId);
                BaseUrlTextBox.Text = _profile.BaseUrl;
                SettingsChanged = true;
                if (closeDialog)
                {
                    DialogResult = true;
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The provider settings could not be saved.\n\n" + ex.Message, "AI Provider", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Provider settings were not saved.";
                return false;
            }
        }


        private bool TryApplyAdditionalHeaders()
        {
            var preserved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in AdditionalHeadersTextBox.Text
                         .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = line.IndexOf('=');
                if (separator <= 0 || separator == line.Length - 1)
                {
                    MessageBox.Show(this, $"Invalid header line: {line}\nUse Header=Value.", "AI Provider", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                var name = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();
                if (IsSensitiveHeaderName(name))
                {
                    MessageBox.Show(this, $"Header '{name}' may contain authentication material and cannot be persisted as an additional header. Use the protected API-key controls instead.", "AI Provider", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                preserved[name] = value;
            }

            _profile.AdditionalHeaders = preserved;
            return true;
        }


        private static bool IsSensitiveHeaderName(string name)
        {
            var normalized = (name ?? string.Empty).Replace("-", string.Empty, StringComparison.Ordinal).Trim();
            return normalized.Contains("authorization", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("apikey", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("token", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("secret", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("credential", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("contenttype", StringComparison.OrdinalIgnoreCase);
        }

        private void SetOptionalHeader(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                _profile.AdditionalHeaders.Remove(name);
            else
                _profile.AdditionalHeaders[name] = value.Trim();
        }

        private void Save_Click(object sender, RoutedEventArgs e) => SaveProfile(closeDialog: true);

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            if (!SaveProfile(closeDialog: false))
                return;

            StatusText.Text = "Testing provider connection…";
            try
            {
                var status = await _providerManagement.GetStatusAsync(_providerId).ConfigureAwait(true);
                if (status.IsAvailable)
                {
                    var modelSummary = status.Models.Count == 0
                        ? status.ErrorMessage ?? "No model IDs were returned; enter an exact model ID manually."
                        : $"{status.Models.Count:N0} model ID(s) discovered.";
                    StatusText.Text = $"Connected to {status.DisplayName} at {status.Endpoint}. {modelSummary}";
                    if (status.Models.Count > 0 && !string.IsNullOrWhiteSpace(status.ErrorMessage))
                        StatusText.Text += " " + status.ErrorMessage;
                }
                else
                {
                    StatusText.Text = $"Connection failed: {status.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Connection test failed: " + ex.Message;
            }
        }

        private void RemoveKey_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this, "Remove the protected API key for this provider?", "AI Provider", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                _providerManagement.DeleteCredential(_providerId);
                ApiKeyPasswordBox.Clear();
                SettingsChanged = true;
                StatusText.Text = "The protected API key was removed.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The protected API key could not be removed.\n\n" + ex.Message, "AI Provider", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "The protected API key was not removed.";
            }
        }
    }
}
