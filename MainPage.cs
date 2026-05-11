using Microsoft.Maui.Controls.Shapes;

namespace NewLauncher;

public sealed class MainPage : ContentPage
{
    private const string LanguageEnglish = "en";
    private const string LanguageJapanese = "ja";
    private const string ThemeSystem = "system";
    private const string ThemeLight = "light";
    private const string ThemeDark = "dark";

    private static Color WindowBackground = Color.FromArgb("#0F1115");
    private static Color SidebarBackground = Color.FromArgb("#13171D");
    private static Color Surface = Color.FromArgb("#191E25");
    private static Color SurfaceSoft = Color.FromArgb("#202732");
    private static Color SurfaceHover = Color.FromArgb("#26313B");
    private static Color SurfacePressed = Color.FromArgb("#1D252D");
    private static Color SurfaceSelected = Color.FromArgb("#20352F");
    private static Color SurfaceSelectedHover = Color.FromArgb("#254238");
    private static Color EmptySurface = Color.FromArgb("#151A20");
    private static Color BadgeSurface = Color.FromArgb("#26303A");
    private static Color EntrySurface = Color.FromArgb("#11161C");
    private static Color EntryHover = Color.FromArgb("#1D2630");
    private static Color EntryFocused = Color.FromArgb("#202D35");
    private static Color Border = Color.FromArgb("#2D3540");
    private static Color BorderHover = Color.FromArgb("#566271");
    private static Color Accent = Color.FromArgb("#48C78E");
    private static Color AccentHover = Color.FromArgb("#5DD9A2");
    private static Color AccentPressed = Color.FromArgb("#37A978");
    private static Color AccentWarm = Color.FromArgb("#E7B955");
    private static Color TextOnAccent = Color.FromArgb("#0A1710");
    private static Color TextPrimary = Color.FromArgb("#F3F6F8");
    private static Color TextSecondary = Color.FromArgb("#B7C0CA");
    private static Color TextMuted = Color.FromArgb("#798592");

    private readonly LauncherStore _store = new();
    private VerticalStackLayout _projectList = null!;
    private VerticalStackLayout _engineList = null!;
    private Label _statusLabel = null!;
    private Label _installProgressLabel = null!;
    private Label _engineUpdateNoticeLabel = null!;
    private Label _engineReleaseStatusLabel = null!;
    private Label _selectedReleaseDetailsLabel = null!;
    private ProgressBar _installProgressBar = null!;
    private Button? _releaseSelectorButton;
    private Label _selectedProjectTitle = null!;
    private Label _selectedProjectMeta = null!;
    private Label _selectedEngineTitle = null!;
    private Label _selectedEngineMeta = null!;
    private Label _projectCountLabel = null!;
    private Label _engineCountLabel = null!;
    private Button? _launchButton;
    private Button? _openProjectButton;
    private Button? _openEngineButton;
    private Button? _openDocumentationButton;
    private Button? _checkReleasesButton;
    private Button? _installReleaseButton;
    private ProjectInfo? _selectedProject;
    private EngineInstallInfo? _selectedEngine;
    private EngineReleaseManifest? _selectedRelease;
    private LauncherView _currentView = LauncherView.Dashboard;
    private string _lastStatusMessage = string.Empty;
    private bool _checkedLauncherUpdate;
    private bool _engineUpdateAvailable;

    public MainPage()
    {
        Title = "New Launcher";
        _store.Load();
        NormalizePreferences();
        ApplyAppearancePreference();
        InitializeControls();
        _selectedProject = _store.Settings.Projects.FirstOrDefault();
        _selectedEngine = _store.Settings.Engines.FirstOrDefault();

        Content = CreateLayout();
        RefreshLists();
        SetStatus(T("Ready.", "準備完了。"));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_checkedLauncherUpdate)
        {
            return;
        }

        _checkedLauncherUpdate = true;
        await CheckLauncherUpdateOnStartupAsync();
    }

    private void InitializeControls()
    {
        _projectList = new VerticalStackLayout { Spacing = 10 };
        _engineList = new VerticalStackLayout { Spacing = 10 };
        _statusLabel = CreateSmallLabel(string.Empty, TextSecondary);
        _installProgressLabel = CreateSmallLabel(T("Waiting for install.", "インストール待機中。"), TextMuted, 11, FontAttributes.Bold);
        _engineUpdateNoticeLabel = CreateSmallLabel(string.Empty, AccentWarm, 13, FontAttributes.Bold);
        _engineUpdateNoticeLabel.IsVisible = false;
        _engineReleaseStatusLabel = CreateSmallLabel(
            T("Startup checks the latest engine automatically.", "起動時に最新エンジンを自動確認します。"),
            TextSecondary);
        _selectedReleaseDetailsLabel = CreateSmallLabel(
            T("Select a release to see details.", "リリースを選択すると詳細を表示します。"),
            TextMuted,
            11);
        _installProgressBar = new ProgressBar
        {
            Progress = 0,
            ProgressColor = Accent,
            BackgroundColor = EntrySurface,
            HeightRequest = 8,
            IsVisible = false
        };
        _releaseSelectorButton = null;
        _selectedProjectTitle = CreateSmallLabel(T("No project selected", "プロジェクト未選択"), TextPrimary, 22, FontAttributes.Bold);
        _selectedProjectMeta = CreateSmallLabel(T("Create or add a project to begin.", "開始するにはプロジェクトを作成または追加してください。"), TextSecondary);
        _selectedEngineTitle = CreateSmallLabel(T("No engine selected", "エンジン未選択"), TextPrimary, 14, FontAttributes.Bold);
        _selectedEngineMeta = CreateSmallLabel(T("Install an engine or use a local development build.", "エンジンをインストールするか、ローカル開発ビルドを使用してください。"), TextMuted, 11);
        _projectCountLabel = CreateSmallLabel(string.Empty, TextPrimary, 18, FontAttributes.Bold);
        _engineCountLabel = CreateSmallLabel(string.Empty, TextPrimary, 18, FontAttributes.Bold);
        _launchButton = null;
        _openProjectButton = null;
        _openEngineButton = null;
        _openDocumentationButton = null;
        _checkReleasesButton = null;
        _installReleaseButton = null;
        BackgroundColor = WindowBackground;
    }

    private void RebuildInterface()
    {
        ApplyAppearancePreference();
        InitializeControls();
        Content = CreateLayout();
        RefreshLists();
        if (!string.IsNullOrWhiteSpace(_lastStatusMessage))
        {
            SetStatus(_lastStatusMessage);
        }
    }

    private View CreateLayout()
    {
        var root = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(248)),
                new ColumnDefinition(GridLength.Star)
            },
            BackgroundColor = WindowBackground
        };

        root.Add(CreateSidebar(), 0, 0);
        root.Add(_currentView == LauncherView.Settings ? CreateSettingsArea() : CreateDashboardArea(), 1, 0);
        return root;
    }

    private View CreateSidebar()
    {
        var stack = new VerticalStackLayout
        {
            Padding = new Thickness(22, 26),
            Spacing = 20,
            BackgroundColor = SidebarBackground
        };

        stack.Children.Add(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                CreateLauncherLogo(),
                new Label { Text = T("Project control center", "プロジェクト管理センター"), TextColor = TextMuted, FontSize = 12 }
            }
        });

        stack.Children.Add(CreateSidebarStat(T("Projects", "プロジェクト"), _projectCountLabel, Accent));
        stack.Children.Add(CreateSidebarStat(T("Engines", "エンジン"), _engineCountLabel, AccentWarm));
        stack.Children.Add(new BoxView { HeightRequest = 1, Color = Border, Margin = new Thickness(0, 4) });
        stack.Children.Add(CreateNavigationButton(T("Dashboard", "ダッシュボード"), LauncherView.Dashboard));
        stack.Children.Add(CreateNavigationButton(T("Settings", "設定"), LauncherView.Settings));
        stack.Children.Add(CreateButton(T("Refresh Library", "ライブラリを更新"), (_, _) => Reload(), ButtonTone.Ghost));
        stack.Children.Add(new Label
        {
            Text = T(
                "Local data is stored under your user profile and cleaned when missing installs or projects disappear.",
                "ローカルデータはユーザープロファイル配下に保存され、存在しないインストールやプロジェクトは整理されます。"),
            TextColor = TextMuted,
            FontSize = 11,
            LineHeight = 1.25
        });

        return stack;
    }

    private Image CreateLauncherLogo()
    {
        return new Image
        {
            Source = ShouldUseLightBrandLogo()
                ? "newengine_launcher_horizontal_light.png"
                : "newengine_launcher_horizontal_dark.png",
            Aspect = Aspect.AspectFit,
            HeightRequest = 86,
            HorizontalOptions = LayoutOptions.Fill
        };
    }

    private Button CreateNavigationButton(string text, LauncherView view)
    {
        ButtonTone tone = _currentView == view ? ButtonTone.Secondary : ButtonTone.Ghost;
        return CreateButton(text, (_, _) =>
        {
            if (_currentView == view)
            {
                return;
            }

            _currentView = view;
            RebuildInterface();
        }, tone);
    }

    private View CreateDashboardArea()
    {
        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Padding = new Thickness(28, 24, 28, 24),
            RowSpacing = 18
        };

        layout.Add(CreateHeroPanel(), 0, 0);
        layout.Add(CreateLibraryGrid(), 0, 1);
        return layout;
    }

    private View CreateHeroPanel()
    {
        _launchButton = CreateButton(T("Launch Project", "プロジェクトを起動"), (_, _) => LaunchSelectedProject(), ButtonTone.Primary);
        _openProjectButton = CreateButton(T("Open Project Folder", "プロジェクトフォルダを開く"), (_, _) => OpenProjectFolder(), ButtonTone.Secondary);

        var hero = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 24
        };

        _openDocumentationButton = CreateButton(T("Open Documentation", "ドキュメントを開く"), (_, _) => OpenDocumentation(), ButtonTone.Ghost);

        hero.Add(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                CreateEyebrow(T("READY TO BUILD", "ビルド準備完了")),
                _selectedProjectTitle,
                _selectedProjectMeta,
                new HorizontalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        CreateButton(T("New Project", "新規プロジェクト"), async (_, _) => await CreateProjectAsync(), ButtonTone.Secondary),
                        CreateButton(T("Add Existing", "既存を追加"), async (_, _) => await AddProjectAsync(), ButtonTone.Ghost),
                        _openProjectButton
                    }
                }
            }
        }, 0, 0);

        hero.Add(new VerticalStackLayout
        {
            Spacing = 12,
            WidthRequest = 250,
            Children =
            {
                CreateSmallLabel(T("Selected engine", "選択中のエンジン"), TextMuted, 11, FontAttributes.Bold),
                _selectedEngineTitle,
                _selectedEngineMeta,
                _launchButton,
                _openDocumentationButton
            }
        }, 1, 0);

        return CreatePanel(hero, new Thickness(22), Accent);
    }

    private View CreateLibraryGrid()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1.12, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(0.95, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.05, GridUnitType.Star))
            },
            ColumnSpacing = 16
        };

        grid.Add(CreatePanel(CreateSection(T("Projects", "プロジェクト"), T("Recent workspaces", "最近のワークスペース"), new View[]
        {
            new ScrollView { Content = _projectList }
        }), new Thickness(16), Border), 0, 0);

        _openEngineButton = CreateButton(T("Open Engine Folder", "エンジンフォルダを開く"), (_, _) => OpenEngineFolder(), ButtonTone.Ghost);
        grid.Add(CreatePanel(CreateSection(T("Engines", "エンジン"), T("Installed runtimes", "インストール済みランタイム"), new View[]
        {
            _openEngineButton,
            new ScrollView { Content = _engineList }
        }), new Thickness(16), Border), 1, 0);

        grid.Add(CreatePanel(CreateSection(T("Updates", "更新"), T("Release channel", "リリースチャンネル"), new View[]
        {
            _engineUpdateNoticeLabel,
            _engineReleaseStatusLabel,
            CreateSmallLabel(T("GitHub release source", "GitHub リリース取得元"), TextMuted, 11, FontAttributes.Bold),
            CreateSmallLabel(_store.ReleaseSourceLabel, TextPrimary, 13, FontAttributes.Bold, maxLines: 2),
            CreateSmallLabel(T("Engine version", "エンジンバージョン"), TextMuted, 11, FontAttributes.Bold),
            _releaseSelectorButton = CreateButton(GetReleaseSelectorText(), async (_, _) => await SelectReleaseAsync(), ButtonTone.Ghost),
            _selectedReleaseDetailsLabel,
            new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 8,
                Children =
                {
                    CreateGridChild(_checkReleasesButton = CreateButton(T("Check Releases", "リリース確認"), async (_, _) => await CheckReleasesAsync(), ButtonTone.Secondary), 0),
                    CreateGridChild(_installReleaseButton = CreateButton(T("Install", "インストール"), async (_, _) => await InstallSelectedReleaseAsync(), ButtonTone.Primary), 1)
                }
            },
            new BoxView { HeightRequest = 1, Color = Border, Margin = new Thickness(0, 6) },
            _installProgressLabel,
            _installProgressBar,
            _statusLabel
        }), new Thickness(16), Border), 2, 0);

        if (_store.LatestRelease != null)
        {
            _engineUpdateAvailable = !_store.IsEngineInstalled(_store.LatestRelease);
        }

        RefreshReleasePicker();
        RefreshEngineUpdateNotice();
        RefreshEngineReleaseStatusFromCache();
        return grid;
    }

    private View CreateSettingsArea()
    {
        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Padding = new Thickness(28, 24, 28, 24),
            RowSpacing = 18
        };

        layout.Add(CreatePanel(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                CreateEyebrow(T("PREFERENCES", "環境設定")),
                CreateSmallLabel(T("Settings", "設定"), TextPrimary, 26, FontAttributes.Bold),
                CreateSmallLabel(
                    T("Choose the launcher language and color behavior. Changes are saved immediately.",
                      "ランチャーの表示言語とカラー動作を選択します。変更はすぐ保存されます。"),
                    TextSecondary)
            }
        }, new Thickness(22), AccentWarm), 0, 0);

        var settingsStack = new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                CreateSettingsRow(
                    T("Display language", "表示言語"),
                    T("Switch all launcher labels between English and Japanese.", "ランチャー内の表示を英語と日本語で切り替えます。"),
                    new View[]
                    {
                        CreateChoiceButton("English", IsLanguage(LanguageEnglish), () => SetLanguage(LanguageEnglish)),
                        CreateChoiceButton("日本語", IsLanguage(LanguageJapanese), () => SetLanguage(LanguageJapanese))
                    }),
                CreateSettingsRow(
                    T("Color theme", "カラーテーマ"),
                    T("System follows the Windows app theme; Light and Dark pin the launcher explicitly.", "システムは Windows のアプリテーマに追従し、ライト/ダークは明示的に固定します。"),
                    new View[]
                    {
                        CreateChoiceButton(T("System", "システム"), IsTheme(ThemeSystem), () => SetColorTheme(ThemeSystem)),
                        CreateChoiceButton(T("Light", "ライト"), IsTheme(ThemeLight), () => SetColorTheme(ThemeLight)),
                        CreateChoiceButton(T("Dark", "ダーク"), IsTheme(ThemeDark), () => SetColorTheme(ThemeDark))
                    }),
                CreatePanel(new VerticalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        CreateSmallLabel(T("Current selection", "現在の設定"), TextPrimary, 14, FontAttributes.Bold),
                        CreateSmallLabel(GetPreferenceSummary(), TextSecondary)
                    }
                }, new Thickness(16), Border)
            }
        };

        layout.Add(settingsStack, 0, 1);
        return layout;
    }

    private View CreateSettingsRow(string title, string subtitle, IEnumerable<View> options)
    {
        var optionStack = new HorizontalStackLayout { Spacing = 10 };
        foreach (View option in options)
        {
            optionStack.Children.Add(option);
        }

        return CreatePanel(new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 18,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        CreateSmallLabel(title, TextPrimary, 15, FontAttributes.Bold),
                        CreateSmallLabel(subtitle, TextMuted, 12)
                    }
                },
                CreateGridChild(optionStack, 1)
            }
        }, new Thickness(16), Border);
    }

    private Button CreateChoiceButton(string text, bool selected, Action selectedAction)
    {
        return CreateButton(text, (_, _) =>
        {
            if (selected)
            {
                return;
            }

            selectedAction();
        }, selected ? ButtonTone.Primary : ButtonTone.Ghost);
    }

    private static View CreateGridChild(View view, int column)
    {
        Grid.SetColumn(view, column);
        return view;
    }

    private static VerticalStackLayout CreateSection(string title, string subtitle, IEnumerable<View> children)
    {
        var stack = new VerticalStackLayout { Spacing = 13 };
        stack.Children.Add(new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                new Label { Text = title, TextColor = TextPrimary, FontSize = 16, FontAttributes = FontAttributes.Bold },
                CreateSubtitle(subtitle)
            }
        });

        foreach (View child in children)
        {
            stack.Children.Add(child);
        }

        return stack;
    }

    private static Label CreateSubtitle(string text)
    {
        var label = CreateSmallLabel(text, TextMuted, 11);
        Grid.SetRow(label, 1);
        return label;
    }

    private static Border CreateSidebarStat(string title, Label valueLabel, Color accent)
    {
        return new Border
        {
            Padding = new Thickness(14, 12),
            BackgroundColor = Surface,
            Stroke = Border,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(4)),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 12,
                Children =
                {
                    new BoxView { Color = accent, WidthRequest = 4, CornerRadius = 2 },
                    CreateStatTextStack(title, valueLabel)
                }
            }
        };
    }

    private static VerticalStackLayout CreateStatTextStack(string title, Label valueLabel)
    {
        var titleLabel = CreateSmallLabel(title, TextMuted, 11, FontAttributes.Bold);
        var stack = new VerticalStackLayout
        {
            Spacing = 2,
            Children = { titleLabel, valueLabel }
        };
        Grid.SetColumn(stack, 1);
        return stack;
    }

    private static Border CreatePanel(View content, Thickness padding, Color stroke)
    {
        return new Border
        {
            Padding = padding,
            BackgroundColor = Surface,
            Stroke = stroke,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = content
        };
    }

    private void RefreshLists()
    {
        RefreshSelectedSummary();

        _projectList.Children.Clear();
        foreach (ProjectInfo project in _store.Settings.Projects)
        {
            _projectList.Children.Add(CreateProjectRow(project));
        }

        if (_store.Settings.Projects.Count == 0)
        {
            _projectList.Children.Add(CreateEmptyRow(
                T("No projects yet.", "プロジェクトはまだありません。"),
                T("Create a project to populate the launcher.", "プロジェクトを作成するとここに表示されます。")));
        }

        _engineList.Children.Clear();
        foreach (EngineInstallInfo engine in _store.Settings.Engines)
        {
            _engineList.Children.Add(CreateEngineRow(engine));
        }

        if (_store.Settings.Engines.Count == 0)
        {
            _engineList.Children.Add(CreateEmptyRow(
                T("No engines installed.", "インストール済みエンジンはありません。"),
                T("Install the latest release or build locally.", "最新版をインストールするかローカルでビルドしてください。")));
        }
    }

    private void RefreshSelectedSummary()
    {
        _projectCountLabel.Text = IsJapanese
            ? $"{_store.Settings.Projects.Count} プロジェクト"
            : $"{_store.Settings.Projects.Count} projects";
        _engineCountLabel.Text = IsJapanese
            ? $"{_store.Settings.Engines.Count} エンジン"
            : $"{_store.Settings.Engines.Count} engines";

        if (_selectedProject == null)
        {
            _selectedProjectTitle.Text = T("No project selected", "プロジェクト未選択");
            _selectedProjectMeta.Text = T("Create a project or add an existing folder to begin.", "開始するにはプロジェクトを作成するか、既存フォルダを追加してください。");
        }
        else
        {
            _selectedProjectTitle.Text = _selectedProject.ProjectName;
            _selectedProjectMeta.Text = IsJapanese
                ? $"{_selectedProject.Path}\nエンジン: {_selectedProject.EngineVersion}  最終起動: {FormatLocalTime(_selectedProject.LastOpenedUtc)}"
                : $"{_selectedProject.Path}\nEngine: {_selectedProject.EngineVersion}  Last opened: {FormatLocalTime(_selectedProject.LastOpenedUtc)}";
        }

        if (_selectedEngine == null)
        {
            _selectedEngineTitle.Text = T("No engine selected", "エンジン未選択");
            _selectedEngineMeta.Text = T("Install an engine or use a local development build.", "エンジンをインストールするか、ローカル開発ビルドを使用してください。");
        }
        else
        {
            _selectedEngineTitle.Text = $"{_selectedEngine.Version} / {_selectedEngine.Channel}";
            _selectedEngineMeta.Text = _selectedEngine.Path;
        }

        bool canLaunch = _selectedProject != null && _selectedEngine != null;
        if (_launchButton != null)
        {
            _launchButton.IsEnabled = canLaunch;
        }

        if (_openProjectButton != null)
        {
            _openProjectButton.IsEnabled = _selectedProject != null;
        }

        if (_openEngineButton != null)
        {
            _openEngineButton.IsEnabled = _selectedEngine != null;
        }

        if (_openDocumentationButton != null)
        {
            _openDocumentationButton.IsEnabled = _selectedEngine != null;
        }

        RefreshInstallButtonState();
    }

    private View CreateProjectRow(ProjectInfo project)
    {
        bool selected = ReferenceEquals(project, _selectedProject) ||
                        string.Equals(project.Path, _selectedProject?.Path, StringComparison.OrdinalIgnoreCase);
        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };

        content.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                CreateSmallLabel(project.ProjectName, TextPrimary, 14, FontAttributes.Bold),
                CreateSmallLabel(project.Path, TextMuted, 11, maxLines: 1),
                CreateSmallLabel(IsJapanese
                    ? $"エンジン {project.EngineVersion} / {FormatLocalTime(project.LastOpenedUtc)}"
                    : $"Engine {project.EngineVersion} / {FormatLocalTime(project.LastOpenedUtc)}", TextSecondary, 11)
            }
        }, 0, 0);
        content.Add(CreateDeleteIconButton(async (_, _) => await DeleteProjectAsync(project)), 1, 0);
        content.Add(CreateSelectionBadge(selected), 2, 0);

        return CreateSelectableRow(selected, content, () =>
        {
            _selectedProject = project;
            RefreshLists();
        });
    }

    private View CreateEngineRow(EngineInstallInfo engine)
    {
        bool selected = ReferenceEquals(engine, _selectedEngine) ||
                        string.Equals(engine.Path, _selectedEngine?.Path, StringComparison.OrdinalIgnoreCase);
        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };

        content.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                CreateSmallLabel(engine.Version, TextPrimary, 14, FontAttributes.Bold),
                CreateSmallLabel(engine.Channel, TextSecondary, 11),
                CreateSmallLabel(engine.Path, TextMuted, 11, maxLines: 1)
            }
        }, 0, 0);
        Button deleteButton = CreateDeleteIconButton(async (_, _) => await DeleteEngineAsync(engine));
        deleteButton.IsEnabled = _store.CanDeleteEngine(engine);
        content.Add(deleteButton, 1, 0);
        content.Add(CreateSelectionBadge(selected), 2, 0);

        return CreateSelectableRow(selected, content, () =>
        {
            _selectedEngine = engine;
            RefreshLists();
        });
    }

    private static Border CreateSelectableRow(bool selected, View content, Action selectedAction)
    {
        var row = new Border
        {
            Padding = new Thickness(12),
            BackgroundColor = selected ? SurfaceSelected : SurfaceSoft,
            Stroke = selected ? Accent : Border,
            StrokeThickness = selected ? 2 : 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = content
        };

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => ApplyRowVisual(row, selected, isHovered: true);
        pointer.PointerExited += (_, _) => ApplyRowVisual(row, selected, isHovered: false);
        row.GestureRecognizers.Add(pointer);
        row.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(selectedAction)
        });
        return row;
    }

    private static void ApplyRowVisual(Border row, bool selected, bool isHovered)
    {
        if (selected)
        {
            row.BackgroundColor = isHovered ? SurfaceSelectedHover : SurfaceSelected;
            row.Stroke = isHovered ? AccentHover : Accent;
            row.StrokeThickness = 2;
            row.Scale = isHovered ? 1.01 : 1;
            return;
        }

        row.BackgroundColor = isHovered ? SurfaceHover : SurfaceSoft;
        row.Stroke = isHovered ? BorderHover : Border;
        row.StrokeThickness = isHovered ? 2 : 1;
        row.Scale = isHovered ? 1.01 : 1;
    }

    private Border CreateSelectionBadge(bool selected)
    {
        return new Border
        {
            Padding = new Thickness(8, 4),
            BackgroundColor = selected ? Accent : BadgeSurface,
            Stroke = selected ? Accent : Border,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            VerticalOptions = LayoutOptions.Start,
            Content = new Label
            {
                Text = selected ? T("Selected", "選択中") : T("Choose", "選択"),
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = selected ? TextOnAccent : TextSecondary
            }
        };
    }

    private static View CreateEmptyRow(string title, string subtitle)
    {
        return new Border
        {
            Padding = new Thickness(14),
            BackgroundColor = EmptySurface,
            Stroke = Border,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    CreateSmallLabel(title, TextSecondary, 13, FontAttributes.Bold),
                    CreateSmallLabel(subtitle, TextMuted, 11)
                }
            }
        };
    }

    private async Task CreateProjectAsync()
    {
        string name = await DisplayPromptAsync(
            T("New Project", "新規プロジェクト"),
            T("Project name", "プロジェクト名"),
            T("Create", "作成"),
            T("Cancel", "キャンセル"),
            "NewProject") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            _selectedProject = _store.CreateProject(name, _selectedEngine);
            RefreshLists();
            SetStatus(IsJapanese ? $"プロジェクトを作成しました: {_selectedProject.ProjectName}" : $"Created project: {_selectedProject.ProjectName}");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(T("Create Project", "プロジェクト作成"), ex.Message, "OK");
        }
    }

    private async Task AddProjectAsync()
    {
        string path = await DisplayPromptAsync(
            T("Add Project", "プロジェクト追加"),
            T("Project folder path", "プロジェクトフォルダのパス"),
            T("Add", "追加"),
            T("Cancel", "キャンセル"),
            _store.DefaultProjectsRoot) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            _selectedProject = _store.AddExistingProject(path, _selectedEngine);
            RefreshLists();
            SetStatus(IsJapanese ? $"プロジェクトを追加しました: {_selectedProject.ProjectName}" : $"Added project: {_selectedProject.ProjectName}");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(T("Add Project", "プロジェクト追加"), ex.Message, "OK");
        }
    }

    private async Task DeleteProjectAsync(ProjectInfo project)
    {
        bool confirmed = await DisplayAlertAsync(
            T("Delete Project", "プロジェクト削除"),
            IsJapanese
                ? $"プロジェクトをフォルダごと削除しますか？\n\n{project.ProjectName}\n{project.Path}"
                : $"Delete this project folder permanently?\n\n{project.ProjectName}\n{project.Path}",
            T("Delete", "削除"),
            T("Cancel", "キャンセル"));
        if (!confirmed)
        {
            return;
        }

        try
        {
            _store.DeleteProject(project);
            _selectedProject = _store.Settings.Projects.FirstOrDefault();
            RefreshLists();
            SetStatus(IsJapanese ? $"プロジェクトを削除しました: {project.ProjectName}" : $"Deleted project: {project.ProjectName}");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(T("Delete Project", "プロジェクト削除"), ex.Message, "OK");
            SetStatus(IsJapanese ? $"プロジェクト削除に失敗しました: {ex.Message}" : $"Project delete failed: {ex.Message}");
        }
    }

    private async Task DeleteEngineAsync(EngineInstallInfo engine)
    {
        if (!_store.CanDeleteEngine(engine))
        {
            SetStatus(T("Local development engines cannot be deleted here.", "ローカル開発エンジンはここでは削除できません。"));
            return;
        }

        bool confirmed = await DisplayAlertAsync(
            T("Delete Engine", "エンジン削除"),
            IsJapanese
                ? $"エンジンをフォルダごと削除しますか？\n\n{engine.Version}\n{engine.Path}"
                : $"Delete this engine folder permanently?\n\n{engine.Version}\n{engine.Path}",
            T("Delete", "削除"),
            T("Cancel", "キャンセル"));
        if (!confirmed)
        {
            return;
        }

        try
        {
            _store.DeleteEngine(engine);
            _selectedEngine = _store.Settings.Engines.FirstOrDefault();
            _engineUpdateAvailable = _store.LatestRelease != null && !_store.IsEngineInstalled(_store.LatestRelease);
            RefreshLists();
            RefreshReleasePicker();
            RefreshEngineUpdateNotice();
            RefreshEngineReleaseStatusFromCache();
            SetStatus(IsJapanese ? $"エンジンを削除しました: {engine.Version}" : $"Deleted engine: {engine.Version}");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(T("Delete Engine", "エンジン削除"), ex.Message, "OK");
            SetStatus(IsJapanese ? $"エンジン削除に失敗しました: {ex.Message}" : $"Engine delete failed: {ex.Message}");
        }
    }

    private void LaunchSelectedProject()
    {
        try
        {
            if (_selectedProject == null || _selectedEngine == null)
            {
                SetStatus(T("Select both a project and an engine.", "プロジェクトとエンジンの両方を選択してください。"));
                return;
            }

            _store.LaunchEditor(_selectedProject, _selectedEngine);
            SetStatus(IsJapanese
                ? $"{_selectedProject.ProjectName} を {_selectedEngine.Version} で起動しました。"
                : $"Launched {_selectedProject.ProjectName} with {_selectedEngine.Version}.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private async Task CheckReleasesAsync()
    {
        try
        {
            SetEngineReleaseStatus(T("Checking engine releases...", "エンジンリリースを確認しています..."), TextSecondary);
            SetStatus(T("Checking releases...", "リリースを確認しています..."));
            IReadOnlyList<EngineReleaseManifest> releases = await _store.CheckAvailableReleasesAsync();
            _selectedRelease = releases.FirstOrDefault() ?? await _store.CheckLatestReleaseAsync();
            _engineUpdateAvailable = _store.LatestRelease != null && !_store.IsEngineInstalled(_store.LatestRelease);
            RefreshReleasePicker();
            RefreshEngineUpdateNotice();
            SetEngineReleaseStatus(GetReleaseCheckStatusMessage(releases.Count), _engineUpdateAvailable ? AccentWarm : TextSecondary);
            SetStatus(IsJapanese
                ? $"{releases.Count} 件のリリースを取得しました。"
                : $"Loaded {releases.Count} releases.");
        }
        catch (Exception ex)
        {
            SetEngineReleaseStatus(
                IsJapanese ? $"エンジンリリース確認に失敗しました: {ex.Message}" : $"Engine release check failed: {ex.Message}",
                AccentWarm);
            SetStatus(ex.Message);
        }
    }

    private async Task InstallSelectedReleaseAsync()
    {
        try
        {
            EngineReleaseManifest release = _selectedRelease ?? _store.LatestRelease ?? await _store.CheckLatestReleaseAsync();
            _selectedRelease = release;
            if (_store.IsEngineInstalled(release))
            {
                RefreshInstallButtonState();
                RefreshSelectedReleaseDetails();
                SetInstallProgress(new EngineInstallProgress(T("Already installed.", "インストール済みです。"), 100));
                SetStatus(IsJapanese ? $"{release.Version} はインストール済みです。" : $"{release.Version} is already installed.");
                return;
            }

            ResetInstallProgress();
            EngineInstallInfo engine = await _store.InstallReleaseAsync(release, new Progress<EngineInstallProgress>(SetInstallProgress));
            _selectedEngine = engine;
            RefreshLists();
            _engineUpdateAvailable = _store.LatestRelease != null && !_store.IsEngineInstalled(_store.LatestRelease);
            RefreshEngineUpdateNotice();
            RefreshSelectedReleaseDetails();
            EngineReleaseManifest statusRelease = _store.LatestRelease ?? release;
            SetEngineReleaseStatus(GetLatestEngineStatusMessage(statusRelease), _engineUpdateAvailable ? AccentWarm : TextSecondary);
            SetInstallProgress(new EngineInstallProgress(T("Install complete.", "インストール完了。"), 100));
            SetStatus(IsJapanese ? $"エンジン {engine.Version} をインストールしました。" : $"Installed engine {engine.Version}.");
        }
        catch (Exception ex)
        {
            _installProgressBar.IsVisible = false;
            _installProgressLabel.Text = T("Install failed.", "インストール失敗。");
            SetStatus(ex.Message);
        }
    }

    private void RefreshReleasePicker()
    {
        if (_releaseSelectorButton == null)
        {
            return;
        }

        IReadOnlyList<EngineReleaseManifest> releases = GetSelectableReleases();
        if (releases.Count == 0)
        {
            _selectedRelease = null;
            _releaseSelectorButton.Text = GetReleaseSelectorText();
            _releaseSelectorButton.IsEnabled = false;
            RefreshInstallButtonState();
            RefreshSelectedReleaseDetails();
            return;
        }

        int selectedIndex = Math.Max(0, releases.ToList().FindIndex(release =>
            string.Equals(release.Version, _selectedRelease?.Version, StringComparison.OrdinalIgnoreCase)));
        _selectedRelease = releases[selectedIndex];
        _releaseSelectorButton.Text = GetReleaseSelectorText();
        _releaseSelectorButton.IsEnabled = true;
        RefreshInstallButtonState();
        RefreshSelectedReleaseDetails();
    }

    private void RefreshInstallButtonState()
    {
        if (_installReleaseButton == null)
        {
            return;
        }

        if (_selectedRelease == null)
        {
            _installReleaseButton.Text = T("Install", "インストール");
            _installReleaseButton.IsEnabled = false;
            return;
        }

        bool installed = _store.IsEngineInstalled(_selectedRelease);
        _installReleaseButton.Text = installed ? T("Installed", "インストール済み") : T("Install", "インストール");
        _installReleaseButton.IsEnabled = !installed;
        if (installed)
        {
            _installProgressLabel.Text = T("Selected version is installed.", "選択中のバージョンはインストール済みです。");
        }
    }

    private void RefreshSelectedReleaseDetails()
    {
        if (_selectedReleaseDetailsLabel == null)
        {
            return;
        }

        if (_selectedRelease == null)
        {
            _selectedReleaseDetailsLabel.Text = T("No engine release selected.", "エンジンリリース未選択です。");
            _selectedReleaseDetailsLabel.TextColor = TextMuted;
            return;
        }

        bool installed = _store.IsEngineInstalled(_selectedRelease);
        string installState = installed ? T("Installed", "インストール済み") : T("Not installed", "未インストール");
        string publishedAt = _selectedRelease.PublishedAtUtc == default
            ? T("unknown", "不明")
            : FormatLocalTime(_selectedRelease.PublishedAtUtc);
        string packageSize = _selectedRelease.SizeBytes > 0
            ? FormatBytesForUi(_selectedRelease.SizeBytes)
            : T("unknown size", "サイズ不明");

        _selectedReleaseDetailsLabel.Text = IsJapanese
            ? $"選択中: {_selectedRelease.Version} / {_selectedRelease.Channel} / {installState}\n公開: {publishedAt} / サイズ: {packageSize}"
            : $"Selected: {_selectedRelease.Version} / {_selectedRelease.Channel} / {installState}\nPublished: {publishedAt} / Size: {packageSize}";
        _selectedReleaseDetailsLabel.TextColor = installed ? TextSecondary : TextPrimary;
    }

    private void SetEngineReleaseStatus(string message, Color color)
    {
        if (_engineReleaseStatusLabel == null)
        {
            return;
        }

        _engineReleaseStatusLabel.Text = message;
        _engineReleaseStatusLabel.TextColor = color;
    }

    private string GetReleaseCheckStatusMessage(int releaseCount)
    {
        if (_store.LatestRelease == null)
        {
            return T("No installable engine release was found.", "インストール可能なエンジンリリースが見つかりません。");
        }

        string latestStatus = _engineUpdateAvailable
            ? T("new engine available", "最新エンジンあり")
            : T("latest installed", "最新インストール済み");
        return IsJapanese
            ? $"確認済み: {releaseCount} 件 / 最新 {_store.LatestRelease.Version} / {latestStatus}"
            : $"Checked: {releaseCount} releases / Latest {_store.LatestRelease.Version} / {latestStatus}";
    }

    private string GetLatestEngineStatusMessage(EngineReleaseManifest latest)
    {
        bool installed = _store.IsEngineInstalled(latest);
        return installed
            ? (IsJapanese ? $"最新エンジンはインストール済みです: {latest.Version}" : $"Latest engine is installed: {latest.Version}")
            : (IsJapanese ? $"最新エンジンがあります: {latest.Version}" : $"New engine available: {latest.Version}");
    }

    private void RefreshEngineReleaseStatusFromCache()
    {
        if (_store.LatestRelease == null)
        {
            return;
        }

        string message = _store.AvailableReleases.Count > 0
            ? GetReleaseCheckStatusMessage(_store.AvailableReleases.Count)
            : GetLatestEngineStatusMessage(_store.LatestRelease);
        SetEngineReleaseStatus(message, _engineUpdateAvailable ? AccentWarm : TextSecondary);
    }

    private void RefreshEngineUpdateNotice()
    {
        if (_engineUpdateNoticeLabel == null)
        {
            return;
        }

        if (!_engineUpdateAvailable || _store.LatestRelease == null)
        {
            _engineUpdateNoticeLabel.IsVisible = false;
            _engineUpdateNoticeLabel.Text = string.Empty;
            return;
        }

        _engineUpdateNoticeLabel.IsVisible = true;
        _engineUpdateNoticeLabel.Text = IsJapanese
            ? $"! 最新エンジンがあります: {_store.LatestRelease.Version}"
            : $"! New engine available: {_store.LatestRelease.Version}";
    }

    private async Task CheckLauncherUpdateOnStartupAsync()
    {
        try
        {
            bool updateStarted = await LauncherUpdateService.CheckAndStartUpdateAsync(new Progress<string>(SetStatus));
            if (updateStarted)
            {
                SetStatus(T("Launcher update downloaded. Restarting...", "Launcher 更新を取得しました。再起動します..."));
                await Task.Delay(500);
                Application.Current?.Quit();
                return;
            }
        }
        catch (Exception ex)
        {
            SetStatus(IsJapanese
                ? $"Launcher 更新確認に失敗しました: {ex.Message}"
                : $"Launcher update check failed: {ex.Message}");
        }

        await CheckEngineUpdateOnStartupAsync();
    }

    private async Task CheckEngineUpdateOnStartupAsync()
    {
        try
        {
            SetEngineReleaseStatus(T("Checking latest engine on startup...", "起動時の最新エンジン確認中..."), TextSecondary);
            SetStatus(T("Checking engine updates...", "エンジン更新を確認しています..."));
            EngineReleaseManifest latest = await _store.CheckLatestReleaseAsync();
            _selectedRelease = latest;
            _engineUpdateAvailable = !_store.IsEngineInstalled(latest);
            RefreshReleasePicker();
            RefreshEngineUpdateNotice();
            SetEngineReleaseStatus(GetLatestEngineStatusMessage(latest), _engineUpdateAvailable ? AccentWarm : TextSecondary);
            SetStatus(GetLatestEngineStatusMessage(latest));
        }
        catch (Exception ex)
        {
            SetEngineReleaseStatus(
                IsJapanese ? $"起動時のエンジン確認に失敗しました: {ex.Message}" : $"Startup engine check failed: {ex.Message}",
                AccentWarm);
            SetStatus(IsJapanese
                ? $"エンジン更新確認に失敗しました: {ex.Message}"
                : $"Engine update check failed: {ex.Message}");
        }
    }

    private void ResetInstallProgress()
    {
        _installProgressBar.Progress = 0;
        _installProgressBar.IsVisible = true;
        _installProgressLabel.Text = T("Preparing install... 0%", "インストール準備中... 0%");
    }

    private void SetInstallProgress(EngineInstallProgress progress)
    {
        if (progress.Percent.HasValue)
        {
            double normalizedProgress = Math.Clamp(progress.Percent.Value / 100d, 0d, 1d);
            _installProgressBar.IsVisible = true;
            _installProgressBar.Progress = normalizedProgress;
            _installProgressLabel.Text = $"{TranslateInstallProgressMessage(progress.Message)} {progress.Percent.Value:0}%";
            SetStatus(TranslateInstallProgressMessage(progress.Message));
            return;
        }

        _installProgressBar.IsVisible = true;
        _installProgressLabel.Text = TranslateInstallProgressMessage(progress.Message);
        SetStatus(TranslateInstallProgressMessage(progress.Message));
    }

    private string TranslateInstallProgressMessage(string message)
    {
        if (!IsJapanese)
        {
            return message;
        }

        if (message.StartsWith("Downloading package", StringComparison.OrdinalIgnoreCase))
        {
            return "パッケージをダウンロード中...";
        }

        return message switch
        {
            "Verifying package..." => "パッケージを検証中...",
            "Extracting package..." => "パッケージを展開中...",
            "Install complete." => "インストール完了。",
            _ => message
        };
    }

    private void Reload()
    {
        _store.Load();
        NormalizePreferences();
        _selectedProject = _store.Settings.Projects.FirstOrDefault(project => string.Equals(project.Path, _selectedProject?.Path, StringComparison.OrdinalIgnoreCase))
            ?? _store.Settings.Projects.FirstOrDefault();
        _selectedEngine = _store.Settings.Engines.FirstOrDefault(engine => string.Equals(engine.Path, _selectedEngine?.Path, StringComparison.OrdinalIgnoreCase))
            ?? _store.Settings.Engines.FirstOrDefault();
        RebuildInterface();
        SetStatus(T("Reloaded.", "再読み込みしました。"));
    }

    private void OpenProjectFolder()
    {
        if (_selectedProject == null)
        {
            SetStatus(T("Select a project first.", "先にプロジェクトを選択してください。"));
            return;
        }

        OpenFolder(_selectedProject.Path);
    }

    private void OpenEngineFolder()
    {
        if (_selectedEngine == null)
        {
            SetStatus(T("Select an engine first.", "先にエンジンを選択してください。"));
            return;
        }

        OpenFolder(_selectedEngine.Path);
    }

    private void OpenDocumentation()
    {
        if (_selectedEngine == null)
        {
            SetStatus(T("Select an engine first.", "先にエンジンを選択してください。"));
            return;
        }

        string? documentationIndexPath = FindDocumentationIndexPath(_selectedEngine);
        if (documentationIndexPath == null)
        {
            SetStatus(T("Documentation was not found. Build the engine docs first.", "ドキュメントが見つかりません。先にエンジンの docs を生成してください。"));
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = documentationIndexPath,
            UseShellExecute = true
        });
        SetStatus(T("Opened documentation.", "ドキュメントを開きました。"));
    }

    private static string? FindDocumentationIndexPath(EngineInstallInfo engine)
    {
        foreach (string candidate in EnumerateDocumentationIndexCandidates(engine))
        {
            if (File.Exists(candidate))
            {
                return System.IO.Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateDocumentationIndexCandidates(EngineInstallInfo engine)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string candidate in new[]
        {
            System.IO.Path.Combine(engine.Path, "docs", "index.html"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "docs", "index.html")
        })
        {
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }

        foreach (string startPath in new[] { engine.Path, AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(System.IO.Path.GetFullPath(startPath));
            while (directory != null)
            {
                string candidate = System.IO.Path.Combine(directory.FullName, "build", "bin", "docs", "index.html");
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }

                directory = directory.Parent;
            }
        }
    }

    private static void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true
        });
    }

    private void SetLanguage(string language)
    {
        _store.Settings.Language = language;
        _store.Save();
        _lastStatusMessage = language == LanguageJapanese ? "表示言語を日本語に変更しました。" : "Display language changed to English.";
        RebuildInterface();
    }

    private void SetColorTheme(string theme)
    {
        _store.Settings.ColorTheme = theme;
        _store.Save();
        _lastStatusMessage = IsJapanese
            ? $"カラーテーマを {GetThemeLabel(theme)} に変更しました。"
            : $"Color theme changed to {GetThemeLabel(theme)}.";
        RebuildInterface();
    }

    private void SetStatus(string message)
    {
        _lastStatusMessage = message;
        _statusLabel.Text = $"[{DateTime.Now:HH:mm:ss}] {message}";
    }

    private bool IsLanguage(string language)
    {
        return string.Equals(_store.Settings.Language, language, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsTheme(string theme)
    {
        return string.Equals(_store.Settings.ColorTheme, theme, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsJapanese => IsLanguage(LanguageJapanese);

    private string T(string english, string japanese)
    {
        return IsJapanese ? japanese : english;
    }

    private string GetPreferenceSummary()
    {
        return IsJapanese
            ? $"表示言語: {GetLanguageLabel(_store.Settings.Language)} / カラーテーマ: {GetThemeLabel(_store.Settings.ColorTheme)}"
            : $"Language: {GetLanguageLabel(_store.Settings.Language)} / Color theme: {GetThemeLabel(_store.Settings.ColorTheme)}";
    }

    private string GetLanguageLabel(string language)
    {
        return string.Equals(language, LanguageJapanese, StringComparison.OrdinalIgnoreCase) ? "日本語" : "English";
    }

    private string GetThemeLabel(string theme)
    {
        return theme.ToLowerInvariant() switch
        {
            ThemeLight => T("Light", "ライト"),
            ThemeDark => T("Dark", "ダーク"),
            _ => T("System", "システム")
        };
    }

    private void NormalizePreferences()
    {
        if (!IsLanguage(LanguageEnglish) && !IsLanguage(LanguageJapanese))
        {
            _store.Settings.Language = LanguageEnglish;
        }

        if (!IsTheme(ThemeSystem) && !IsTheme(ThemeLight) && !IsTheme(ThemeDark))
        {
            _store.Settings.ColorTheme = ThemeSystem;
        }
    }

    private static Button CreateButton(string text, EventHandler clicked, ButtonTone tone)
    {
        ButtonPalette palette = GetButtonPalette(tone);
        var button = new Button
        {
            Text = text,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(14, 8),
            BackgroundColor = palette.NormalBackground,
            TextColor = palette.NormalText,
            BorderColor = palette.NormalBorder,
            BorderWidth = 1,
            CornerRadius = 7,
            MinimumHeightRequest = 36
        };
        button.Clicked += clicked;
        ApplyButtonVisualStates(button, palette);
        return button;
    }

    private static Button CreateDeleteIconButton(EventHandler clicked)
    {
        Button button = CreateButton("×", clicked, ButtonTone.Danger);
        button.FontSize = 16;
        button.Padding = new Thickness(0);
        button.WidthRequest = 30;
        button.HeightRequest = 30;
        button.MinimumWidthRequest = 30;
        button.MinimumHeightRequest = 30;
        button.CornerRadius = 6;
        button.HorizontalOptions = LayoutOptions.End;
        button.VerticalOptions = LayoutOptions.Start;
        SemanticProperties.SetDescription(button, "Delete");
        return button;
    }

    private static Entry CreateEntry(string placeholder)
    {
        var entry = new Entry
        {
            Placeholder = placeholder,
            FontSize = 12,
            TextColor = TextPrimary,
            PlaceholderColor = TextMuted,
            BackgroundColor = EntrySurface,
            HeightRequest = 38,
            Margin = new Thickness(0)
        };
        ApplyEntryVisualStates(entry);
        return entry;
    }

    private async Task SelectReleaseAsync()
    {
        IReadOnlyList<EngineReleaseManifest> releases = GetSelectableReleases();
        if (releases.Count == 0)
        {
            SetStatus(T("Check releases first.", "先にリリース確認をしてください。"));
            return;
        }

        string[] choices = releases.Select(FormatReleaseChoice).ToArray();
        string cancel = T("Cancel", "キャンセル");
        string? selectedChoice = await DisplayActionSheetAsync(
            T("Select engine version", "エンジンバージョンを選択"),
            cancel,
            null,
            choices);
        if (string.IsNullOrWhiteSpace(selectedChoice) || string.Equals(selectedChoice, cancel, StringComparison.Ordinal))
        {
            return;
        }

        int selectedIndex = Array.IndexOf(choices, selectedChoice);
        if (selectedIndex < 0 || selectedIndex >= releases.Count)
        {
            return;
        }

        _selectedRelease = releases[selectedIndex];
        RefreshReleasePicker();
        SetStatus(IsJapanese
            ? $"エンジン {_selectedRelease.Version} を選択しました。"
            : $"Selected engine {_selectedRelease.Version}.");
    }

    private IReadOnlyList<EngineReleaseManifest> GetSelectableReleases()
    {
        return _store.AvailableReleases.Count > 0
            ? _store.AvailableReleases
            : _store.LatestRelease == null ? Array.Empty<EngineReleaseManifest>() : new[] { _store.LatestRelease };
    }

    private string GetReleaseSelectorText()
    {
        return _selectedRelease == null
            ? T("Check releases first", "先にリリース確認")
            : $"{FormatReleaseChoice(_selectedRelease)}  v";
    }

    private string FormatReleaseChoice(EngineReleaseManifest release)
    {
        string installedSuffix = _store.IsEngineInstalled(release)
            ? T(" / installed", " / インストール済み")
            : string.Empty;
        return $"{release.Version} ({release.Channel}){installedSuffix}";
    }

    private static Label CreateEyebrow(string text)
    {
        return CreateSmallLabel(text, AccentWarm, 11, FontAttributes.Bold);
    }

    private static Label CreateSmallLabel(
        string text,
        Color color,
        double fontSize = 12,
        FontAttributes attributes = FontAttributes.None,
        int maxLines = 0)
    {
        var label = new Label
        {
            Text = text,
            TextColor = color,
            FontSize = fontSize,
            FontAttributes = attributes,
            LineBreakMode = maxLines == 1 ? LineBreakMode.MiddleTruncation : LineBreakMode.WordWrap
        };

        if (maxLines > 0)
        {
            label.MaxLines = maxLines;
        }

        return label;
    }

    private static string FormatLocalTime(DateTime utcTime)
    {
        if (utcTime == default)
        {
            return "never";
        }

        return utcTime.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
    }

    private static string FormatBytesForUi(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        int unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.#} {units[unitIndex]}";
    }

    private void ApplyAppearancePreference()
    {
        string colorTheme = _store.Settings.ColorTheme.ToLowerInvariant();
        Application.Current!.UserAppTheme = colorTheme switch
        {
            ThemeLight => AppTheme.Light,
            ThemeDark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };

        bool useLightPalette = colorTheme == ThemeLight ||
            (colorTheme == ThemeSystem && Application.Current.RequestedTheme == AppTheme.Light);
        if (useLightPalette)
        {
            ApplyLightPalette();
            return;
        }

        ApplyDarkPalette();
    }

    private bool ShouldUseLightBrandLogo()
    {
        string colorTheme = _store.Settings.ColorTheme.ToLowerInvariant();
        return colorTheme == ThemeLight ||
            (colorTheme == ThemeSystem && Application.Current?.RequestedTheme == AppTheme.Light);
    }

    private static void ApplyDarkPalette()
    {
        WindowBackground = Color.FromArgb("#0F1115");
        SidebarBackground = Color.FromArgb("#13171D");
        Surface = Color.FromArgb("#191E25");
        SurfaceSoft = Color.FromArgb("#202732");
        SurfaceHover = Color.FromArgb("#26313B");
        SurfacePressed = Color.FromArgb("#1D252D");
        SurfaceSelected = Color.FromArgb("#20352F");
        SurfaceSelectedHover = Color.FromArgb("#254238");
        EmptySurface = Color.FromArgb("#151A20");
        BadgeSurface = Color.FromArgb("#26303A");
        EntrySurface = Color.FromArgb("#11161C");
        EntryHover = Color.FromArgb("#1D2630");
        EntryFocused = Color.FromArgb("#202D35");
        Border = Color.FromArgb("#2D3540");
        BorderHover = Color.FromArgb("#566271");
        Accent = Color.FromArgb("#48C78E");
        AccentHover = Color.FromArgb("#5DD9A2");
        AccentPressed = Color.FromArgb("#37A978");
        AccentWarm = Color.FromArgb("#E7B955");
        TextOnAccent = Color.FromArgb("#0A1710");
        TextPrimary = Color.FromArgb("#F3F6F8");
        TextSecondary = Color.FromArgb("#B7C0CA");
        TextMuted = Color.FromArgb("#798592");
    }

    private static void ApplyLightPalette()
    {
        WindowBackground = Color.FromArgb("#F5F7FA");
        SidebarBackground = Color.FromArgb("#E9EEF3");
        Surface = Color.FromArgb("#FFFFFF");
        SurfaceSoft = Color.FromArgb("#EDF2F7");
        SurfaceHover = Color.FromArgb("#E0E9F0");
        SurfacePressed = Color.FromArgb("#D5E1EA");
        SurfaceSelected = Color.FromArgb("#DDF4EA");
        SurfaceSelectedHover = Color.FromArgb("#C8EBD9");
        EmptySurface = Color.FromArgb("#F2F5F8");
        BadgeSurface = Color.FromArgb("#E1E8EF");
        EntrySurface = Color.FromArgb("#F3F6F9");
        EntryHover = Color.FromArgb("#E7EEF5");
        EntryFocused = Color.FromArgb("#E0F0EA");
        Border = Color.FromArgb("#CBD5DF");
        BorderHover = Color.FromArgb("#647482");
        Accent = Color.FromArgb("#26A66F");
        AccentHover = Color.FromArgb("#1B8F5C");
        AccentPressed = Color.FromArgb("#16764E");
        AccentWarm = Color.FromArgb("#B7791F");
        TextOnAccent = Color.FromArgb("#FFFFFF");
        TextPrimary = Color.FromArgb("#111827");
        TextSecondary = Color.FromArgb("#4B5563");
        TextMuted = Color.FromArgb("#6B7280");
    }

    private static ButtonPalette GetButtonPalette(ButtonTone tone)
    {
        return tone switch
        {
            ButtonTone.Primary => new ButtonPalette(Accent, AccentHover, AccentPressed, TextOnAccent, TextOnAccent, Accent),
            ButtonTone.Secondary => new ButtonPalette(SurfaceSoft, SurfaceHover, SurfacePressed, TextPrimary, TextPrimary, Accent),
            ButtonTone.Ghost => new ButtonPalette(Surface, SurfaceHover, SurfacePressed, TextSecondary, TextPrimary, Border),
            ButtonTone.Danger => new ButtonPalette(SurfaceSoft, SurfaceHover, SurfacePressed, TextMuted, AccentWarm, Border),
            _ => new ButtonPalette(Surface, SurfaceHover, SurfacePressed, TextSecondary, TextPrimary, Border)
        };
    }

    private static void ApplyButtonVisualStates(Button button, ButtonPalette palette)
    {
        VisualStateManager.GetVisualStateGroups(button).Add(new VisualStateGroup
        {
            Name = "CommonStates",
            States =
            {
                new VisualState
                {
                    Name = "Normal",
                    Setters =
                    {
                        new Setter { Property = Button.BackgroundColorProperty, Value = palette.NormalBackground },
                        new Setter { Property = Button.TextColorProperty, Value = palette.NormalText },
                        new Setter { Property = Button.BorderColorProperty, Value = palette.NormalBorder },
                        new Setter { Property = Button.BorderWidthProperty, Value = 1.0 },
                        new Setter { Property = Button.ScaleProperty, Value = 1.0 },
                        new Setter { Property = Button.OpacityProperty, Value = 1.0 }
                    }
                },
                new VisualState
                {
                    Name = "PointerOver",
                    Setters =
                    {
                        new Setter { Property = Button.BackgroundColorProperty, Value = palette.HoverBackground },
                        new Setter { Property = Button.TextColorProperty, Value = palette.HoverText },
                        new Setter { Property = Button.BorderColorProperty, Value = BorderHover },
                        new Setter { Property = Button.ScaleProperty, Value = 1.02 }
                    }
                },
                new VisualState
                {
                    Name = "Pressed",
                    Setters =
                    {
                        new Setter { Property = Button.BackgroundColorProperty, Value = palette.PressedBackground },
                        new Setter { Property = Button.ScaleProperty, Value = 0.98 }
                    }
                },
                new VisualState
                {
                    Name = "Focused",
                    Setters =
                    {
                        new Setter { Property = Button.BorderColorProperty, Value = AccentWarm },
                        new Setter { Property = Button.BorderWidthProperty, Value = 2.0 }
                    }
                },
                new VisualState
                {
                    Name = "Disabled",
                    Setters =
                    {
                        new Setter { Property = Button.BackgroundColorProperty, Value = SurfacePressed },
                        new Setter { Property = Button.TextColorProperty, Value = TextMuted },
                        new Setter { Property = Button.BorderColorProperty, Value = Border },
                        new Setter { Property = Button.OpacityProperty, Value = 0.58 }
                    }
                }
            }
        });
    }

    private static void ApplyEntryVisualStates(Entry entry)
    {
        VisualStateManager.GetVisualStateGroups(entry).Add(new VisualStateGroup
        {
            Name = "CommonStates",
            States =
            {
                new VisualState
                {
                    Name = "Normal",
                    Setters =
                    {
                        new Setter { Property = Entry.BackgroundColorProperty, Value = EntrySurface },
                        new Setter { Property = Entry.TextColorProperty, Value = TextPrimary }
                    }
                },
                new VisualState
                {
                    Name = "PointerOver",
                    Setters =
                    {
                        new Setter { Property = Entry.BackgroundColorProperty, Value = EntryHover }
                    }
                },
                new VisualState
                {
                    Name = "Focused",
                    Setters =
                    {
                        new Setter { Property = Entry.BackgroundColorProperty, Value = EntryFocused },
                        new Setter { Property = Entry.TextColorProperty, Value = TextPrimary }
                    }
                },
                new VisualState
                {
                    Name = "Disabled",
                    Setters =
                    {
                        new Setter { Property = Entry.BackgroundColorProperty, Value = SurfacePressed },
                        new Setter { Property = Entry.TextColorProperty, Value = TextMuted }
                    }
                }
            }
        });
    }

    private enum ButtonTone
    {
        Primary,
        Secondary,
        Ghost,
        Danger
    }

    private enum LauncherView
    {
        Dashboard,
        Settings
    }

    private readonly record struct ButtonPalette(
        Color NormalBackground,
        Color HoverBackground,
        Color PressedBackground,
        Color NormalText,
        Color HoverText,
        Color NormalBorder);
}
