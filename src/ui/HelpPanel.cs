namespace ZombieWars.UI;

using Godot;
using System.Collections.Generic;
using ZombieWars.Core;
using static ZombieWars.Core.GameConfig;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>
/// Tabbed help panel — HUD legend, controls, zombie codex, weapon guide, and game mechanics.
/// Toggled via H key or the "?" HUD button.
///
/// 设计原则:极简可读,只用 2 个颜色 + 字号层级区分信息,无装饰色块/边框/banner。
/// - 章节标题(15px 粗 BattlefieldBright) + HSeparator 分隔
/// - 条目(13px NoteText)左对齐自动换行
/// - 强 outline (InkBlack 1-2) 保证任何背景下都清晰
/// </summary>
public partial class HelpPanel : Panel
{
    private int _currentTab;
    private readonly string[] _tabNames = { "HUD", "操作", "僵尸", "武器", "机制" };
    private readonly List<Button> _tabButtons = new();
    private VBoxContainer _contentContainer = null!;

    public override void _Ready()
    {
        var viewport = ResponsiveUi.ViewportSize(this);

        Visible = false;
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        // 暗色背景遮罩(无 Panel 装饰)
        var bg = new ColorRect
        {
            Color = new Color(BattlefieldBg, 0.82f),
            MouseFilter = MouseFilterEnum.Stop
        };
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var card = new Panel();
        card.CustomMinimumSize = ResponsiveUi.FitPanel(new Vector2(720, 580), viewport);
        card.MouseFilter = MouseFilterEnum.Stop;
        var cardStyle = MakeNotePanel(borderRadius: 6f, shadowSize: 8f);
        cardStyle.ShadowOffset = new Vector2(3, 3);
        card.AddThemeStyleboxOverride("panel", cardStyle);
        center.AddChild(card);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", ResponsiveUi.Px(28, viewport, 14));
        margin.AddThemeConstantOverride("margin_right", ResponsiveUi.Px(28, viewport, 14));
        margin.AddThemeConstantOverride("margin_top", ResponsiveUi.Px(18, viewport, 10));
        margin.AddThemeConstantOverride("margin_bottom", ResponsiveUi.Px(18, viewport, 10));
        card.AddChild(margin);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", ResponsiveUi.Px(10, viewport, 6));
        margin.AddChild(mainVbox);

        // 标题行:左标题 + 右关闭提示
        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", ResponsiveUi.Px(8, viewport, 4));
        mainVbox.AddChild(titleRow);

        var titleLabel = new Label
        {
            Text = "帮助与说明",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        titleLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontTitle, viewport, 18));
        titleLabel.AddThemeColorOverride("font_color", NoteText);
        titleLabel.AddThemeConstantOverride("outline_size", 2);
        titleLabel.AddThemeColorOverride("font_outline_color", InkBlack);
        titleRow.AddChild(titleLabel);

        var closeHint = new Label { Text = "H / ESC 关闭" };
        closeHint.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontSmall, viewport, 10));
        closeHint.AddThemeColorOverride("font_color", new Color(NoteText, 0.65f));
        closeHint.VerticalAlignment = VerticalAlignment.Center;
        titleRow.AddChild(closeHint);

        // Tab 栏(简化样式,只用 2 色)
        var tabBar = new HBoxContainer();
        tabBar.AddThemeConstantOverride("separation", ResponsiveUi.Px(4, viewport, 2));
        mainVbox.AddChild(tabBar);

        for (int i = 0; i < _tabNames.Length; i++)
        {
            var idx = i;
            var btn = new Button
            {
                Text = _tabNames[i],
                CustomMinimumSize = new Vector2(0, ResponsiveUi.Px(28, viewport, 22)),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.None
            };
            btn.Pressed += () => _SwitchTab(idx);
            _StyleTabButton(btn, i == 0);
            tabBar.AddChild(btn);
            _tabButtons.Add(btn);
        }

        // 章节分隔线
        mainVbox.AddChild(new HSeparator { Modulate = new Color(NoteBorder, 0.5f) });

        // ScrollContainer(无装饰面板)
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.MouseFilter = MouseFilterEnum.Pass;
        mainVbox.AddChild(scroll);

        _contentContainer = new VBoxContainer();
        _contentContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _contentContainer.AddThemeConstantOverride("separation", ResponsiveUi.Px(4, viewport, 2));
        scroll.AddChild(_contentContainer);

        _currentTab = 0;
        _BuildContent();
    }

    public void Toggle() { Visible = !Visible; }
    public void Close() { Visible = false; }

    private void _SwitchTab(int index)
    {
        if (index == _currentTab) return;
        _currentTab = index;
        for (int i = 0; i < _tabButtons.Count; i++)
            _StyleTabButton(_tabButtons[i], i == index);
        _BuildContent();
    }

    private void _StyleTabButton(Button btn, bool active)
    {
        var viewport = ResponsiveUi.ViewportSize(this);

        var normal = new StyleBoxFlat
        {
            BgColor = active ? new Color(NoteText, 0.85f) : new Color(NoteText, 0.15f),
            BorderColor = active ? NoteBorder : new Color(NoteBorder, 0.3f),
            BorderWidthBottom = active ? 2 : 1,
            ContentMarginLeft = ResponsiveUi.Px(8, viewport, 4),
            ContentMarginRight = ResponsiveUi.Px(8, viewport, 4),
            ContentMarginTop = ResponsiveUi.Px(2, viewport, 1),
            ContentMarginBottom = ResponsiveUi.Px(2, viewport, 1),
        };
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", normal);
        btn.AddThemeStyleboxOverride("pressed", normal);
        btn.AddThemeColorOverride("font_color", active ? NoteBg : new Color(NoteText, 0.85f));
        btn.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontBody, viewport, 12));
    }

    private void _BuildContent()
    {
        foreach (var child in _contentContainer.GetChildren())
            child.QueueFree();

        switch (_currentTab)
        {
            case 0: _BuildHudTab(); break;
            case 1: _BuildControlsTab(); break;
            case 2: _BuildZombieTab(); break;
            case 3: _BuildWeaponTab(); break;
            case 4: _BuildMechanicsTab(); break;
        }
    }

    // ═══ 通用辅助:_AddChapter / _AddEntry ═══

    private void _AddChapter(string text)
    {
        var viewport = ResponsiveUi.ViewportSize(this);

        var spacer = new Control { CustomMinimumSize = new Vector2(0, ResponsiveUi.Px(6, viewport, 3)) };
        _contentContainer.AddChild(spacer);

        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontEmph, viewport, 14));
        label.AddThemeColorOverride("font_color", NoteText);
        label.AddThemeConstantOverride("outline_size", 2);
        label.AddThemeColorOverride("font_outline_color", InkBlack);
        _contentContainer.AddChild(label);

        _contentContainer.AddChild(new HSeparator { Modulate = new Color(NoteBorder, 0.4f) });
    }

    /// <summary>添加一行 "标题: 描述"。标题用 NoteText 粗体,描述用 NoteText 浅色。</summary>
    private void _AddEntry(string title, string content)
    {
        var viewport = ResponsiveUi.ViewportSize(this);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", ResponsiveUi.Px(6, viewport, 3));
        _contentContainer.AddChild(row);

        var titleLabel = new Label
        {
            Text = title,
            CustomMinimumSize = new Vector2(ResponsiveUi.Px(80, viewport, 60), 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        titleLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontBody, viewport, 12));
        titleLabel.AddThemeColorOverride("font_color", NoteText);
        titleLabel.AddThemeConstantOverride("outline_size", 1);
        titleLabel.AddThemeColorOverride("font_outline_color", InkBlack);
        row.AddChild(titleLabel);

        var contentLabel = new Label
        {
            Text = content,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Top
        };
        contentLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontBody, viewport, 12));
        contentLabel.AddThemeColorOverride("font_color", new Color(NoteText, 0.78f));
        row.AddChild(contentLabel);
    }

    // ═══════════════════════════════════════════════
    // Tab: HUD
    // ═══════════════════════════════════════════════

    private void _BuildHudTab()
    {
        _AddChapter("顶部信息栏");
        _AddEntry("回合",   "当前第几回合。每回合开始前抽强化卡,回合越高敌人越强。");
        _AddEntry("僵尸",   "剩余/本回合总量。清完进入下一回合。");
        _AddEntry("天气",   "当前天气。危险天气造成持续掉血或额外刷怪。");
        _AddEntry("负面",   "中毒/燃烧等持续掉血状态,会显示数值例如 -5/s。");
        _AddEntry("分数",   "击杀僵尸、精英和 BOSS 获得。BOSS 分数最高。");

        _AddChapter("底部战斗栏");
        _AddEntry("效果",   "当前武器、装备、护盾、暴击、吸血等图标。鼠标悬停查看详情。");
        _AddEntry("生命",   "归零死亡;低血量时血条闪烁。回合结算时恢复部分生命。");
        _AddEntry("闪避",   "就绪时按 Shift 冲刺;冷却中显示剩余秒数。");
        _AddEntry("武器",   "当前主武器名称 + 攻击类型(近战/远程/范围/魔法)。");
        _AddEntry("目标",   "自动攻击优先目标:最近/最远/密集。按 Tab 切换。");
        _AddEntry("伤害/间隔", "单次攻击基础伤害;两次攻击之间时间。");
        _AddEntry("暴击/吸血/移速", "暴击爆发,吸血回血,移速影响走位。");

        _AddChapter("特殊系统");
        _AddEntry("力场",   "左下显示力场次数。按 ` 放置,左键放,右键取消。");
        _AddEntry("共振",   "右上指示武器频率与尸潮波形的接近程度。越接近加成越大。");
        _AddEntry("装备",   "按 C 或点底部【装备】打开,查看身体部位槽位。");
    }

    // ═══════════════════════════════════════════════
    // Tab: Controls
    // ═══════════════════════════════════════════════

    private void _BuildControlsTab()
    {
        _AddChapter("移动与攻击");
        _AddEntry("WASD / 方向键", "移动角色");
        _AddEntry("空格 (按住)",   "持续攻击;魔法类自动锁定目标");
        _AddEntry("Shift",         "冲刺闪避,有冷却时间");
        _AddEntry("Tab",           "切换目标优先级 (最近/最远/密集)");

        _AddChapter("游戏控制");
        _AddEntry("ESC",  "暂停/继续游戏。强化阶段无法手动暂停。");
        _AddEntry("H",    "打开/关闭帮助面板");
        _AddEntry("C",    "打开/关闭角色装备面板");

        _AddChapter("强化阶段");
        _AddEntry("1 / 2 / 3",      "武器选择阶段快速选牌");
        _AddEntry("Enter / Delete", "武器选择阶段跳过强化");
    }

    // ═══════════════════════════════════════════════
    // Tab: Zombie Codex
    // ═══════════════════════════════════════════════

    private void _BuildZombieTab()
    {
        _AddChapter("基础僵尸");
        _AddEntry("基础",   "数量多但个体较弱");
        _AddEntry("快速",   "移动速度快,难以甩脱");
        _AddEntry("坦克",   "血量厚、伤害高,行动缓慢");
        _AddEntry("爆炸",   "死亡时爆炸,造成范围伤害");
        _AddEntry("隐身",   "可以隐身,定期显形");
        _AddEntry("喷毒",   "远距离发射毒液弹,造成持续伤害");
        _AddEntry("治疗",   "会治愈附近僵尸,应优先击杀");
        _AddEntry("召唤师", "召唤小僵尸助战");
        _AddEntry("射手",   "远程射箭攻击");
        _AddEntry("飞船",   "空中单位,成群出现");
        _AddEntry("机甲",   "重型装甲,非常耐打");
        _AddEntry("骑士",   "攻守兼备的精英单位");
        _AddEntry("飞龙",   "强力空中单位,喷火攻击");
        _AddEntry("虫群",   "小型虫类,单体弱但数量多");

        _AddChapter("精英前缀");
        _AddEntry("再生", "精英缓慢回复生命值");
        _AddEntry("迅捷", "精英移动速度大幅提升");
        _AddEntry("爆炸", "精英死亡时产生爆炸");

        _AddChapter("BOSS");
        _AddEntry("BOSS", "每 5 回合出现一次。体型巨大、属性强化为普通数倍,击杀高分。");
    }

    // ═══════════════════════════════════════════════
    // Tab: Weapons
    // ═══════════════════════════════════════════════

    private void _BuildWeaponTab()
    {
        _AddChapter("武器类型");
        _AddEntry("近战", "扇形斩击近距离敌人;可搭配溅射、多目标、击退");
        _AddEntry("远程", "发射子弹打远处敌人;可搭配多射、穿透、轨迹");
        _AddEntry("魔法", "追踪导弹自动锁定;可搭配魅惑、推力、链式闪电");
        _AddEntry("范围", "爆炸范围伤害,适合清理密集敌群");

        _AddChapter("装备");
        _AddEntry("装备",   "被动增强角色属性,不替换武器,可与武器叠加");
        _AddEntry("防御类", "反伤:反弹伤害给攻击者;闪避:完全躲避;减控:缩短负面持续");
        _AddEntry("攻击类", "穿透:贯穿更多敌人;弹跳:反弹多个目标;溅射:波及周围");

        _AddChapter("武器特效");
        _AddEntry("近战", "溅射/多目标/击杀爆炸/击杀护盾/击退");
        _AddEntry("远程", "多射/轨迹伤害/回旋/穿透");
        _AddEntry("魔法", "魅惑/推力/眩晕/链式/中毒");

        _AddChapter("品质与强化");
        _AddEntry("品质", "普通(灰) < 稀有(红) < 史诗(金) < 传说");
        _AddEntry("强化", "选中已有装备的强化卡,直接提升属性和特效");
    }

    // ═══════════════════════════════════════════════
    // Tab: Game Mechanics
    // ═══════════════════════════════════════════════

    private void _BuildMechanicsTab()
    {
        _AddChapter("回合流程");
        _AddEntry("循环", "开始 → 选卡 → 战斗 → 结算。回合间恢复 20% 最大生命。");

        _AddChapter("战斗机制");
        _AddEntry("连击", "连续击杀积累连击,加成最高 +50%。停止击杀会衰减。");
        _AddEntry("暴击", "2.5x 伤害,装备可提升暴击率(上限 75%)");
        _AddEntry("吸血", "命中后回复伤害值一定比例(上限 40%)");
        _AddEntry("护盾", "装备护盾后,环绕的冰晶伤害并减速接近的敌人");

        _AddChapter("状态效果");
        _AddEntry("冰冻", "大幅降低敌人移速,持续约 0.8 秒");
        _AddEntry("中毒", "持续伤害,持续约 3 秒");
        _AddEntry("魅惑", "使敌人互相攻击,持续一段时间");
        _AddEntry("眩晕", "短暂使敌人无法行动");

        _AddChapter("其他系统");
        _AddEntry("掉落", "生命药水回血;技能书在精英和 BOSS 掉落概率更高");
        _AddEntry("天气", "暴风天气时可能额外生成僵尸");
        _AddEntry("成就", "完成特定目标解锁,首杀/存活/连击记录保存到存档");
    }
}
