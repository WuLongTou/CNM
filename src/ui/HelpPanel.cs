namespace ZombieWars.UI;

using Godot;
using System.Collections.Generic;
using ZombieWars.Core;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>
/// Tabbed help panel — HUD legend, controls, zombie codex, weapon guide, and game mechanics.
/// Toggled via H key or the "?" HUD button.
/// </summary>
public partial class HelpPanel : Panel
{
    private int _currentTab;
    private readonly string[] _tabNames = { "HUD", "操作", "僵尸", "武器", "机制" };
    private Panel _contentPanel = null!;
    private VBoxContainer _contentContainer = null!;
    private readonly List<Button> _tabButtons = new();

    public override void _Ready()
    {
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var bgStyle = new StyleBoxFlat
        {
            BgColor = new Color(BattlefieldBg, 0.75f)
        };
        AddThemeStyleboxOverride("panel", bgStyle);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var card = new Panel();
        card.CustomMinimumSize = ResponsiveUi.FitPanel(new Vector2(700, 560), ResponsiveUi.ViewportSize(this));
        card.MouseFilter = MouseFilterEnum.Stop;
        var cardStyle = MakeNotePanel(borderRadius: 8f, shadowSize: 10f);
        cardStyle.ShadowOffset = new Vector2(4, 4);
        card.AddThemeStyleboxOverride("panel", cardStyle);
        center.AddChild(card);

        var outerMargin = new MarginContainer();
        outerMargin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        outerMargin.AddThemeConstantOverride("margin_left", 16);
        outerMargin.AddThemeConstantOverride("margin_right", 16);
        outerMargin.AddThemeConstantOverride("margin_top", 12);
        outerMargin.AddThemeConstantOverride("margin_bottom", 12);
        outerMargin.MouseFilter = MouseFilterEnum.Ignore;
        card.AddChild(outerMargin);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", 8);
        outerMargin.AddChild(mainVbox);

        // Title row
        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 8);
        mainVbox.AddChild(titleRow);

        var titleLabel = new Label
        {
            Text = "📖 帮助与说明",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 24);
        titleLabel.AddThemeColorOverride("font_color", BattlefieldBright);
        titleLabel.AddThemeConstantOverride("outline_size", 3);
        titleLabel.AddThemeColorOverride("font_outline_color", InkBlack);
        titleRow.AddChild(titleLabel);

        var closeHint = new Label
        {
            Text = "[ H / ESC 关闭 ]"
        };
        closeHint.AddThemeFontSizeOverride("font_size", 12);
        closeHint.AddThemeColorOverride("font_color", new Color(NoteText, 0.7f));
        closeHint.VerticalAlignment = VerticalAlignment.Center;
        titleRow.AddChild(closeHint);

        // Tab bar
        var tabBar = new HBoxContainer();
        tabBar.AddThemeConstantOverride("separation", 4);
        mainVbox.AddChild(tabBar);

        for (int i = 0; i < _tabNames.Length; i++)
        {
            var idx = i;
            var btn = new Button
            {
                Text = _tabNames[i],
                CustomMinimumSize = new Vector2(0, 32),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.None
            };
            btn.Pressed += () => _SwitchTab(idx);
            _StyleTabButton(btn, i == 0);
            tabBar.AddChild(btn);
            _tabButtons.Add(btn);
        }

        // Content area
        _contentPanel = new Panel();
        _contentPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        _contentPanel.MouseFilter = MouseFilterEnum.Ignore;
        var contentStyle = new StyleBoxFlat
        {
            BgColor = new Color(NoteBg, 0.3f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        _contentPanel.AddThemeStyleboxOverride("panel", contentStyle);
        mainVbox.AddChild(_contentPanel);

        var scroll = new ScrollContainer();
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.MouseFilter = MouseFilterEnum.Pass;
        _contentPanel.AddChild(scroll);

        _contentContainer = new VBoxContainer();
        _contentContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _contentContainer.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_contentContainer);

        _currentTab = 0;
        _BuildContent();
    }

    public void Toggle()
    {
        Visible = !Visible;
    }

    public void Close()
    {
        Visible = false;
    }

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
        var normal = new StyleBoxFlat
        {
            BgColor = active ? new Color(BloodBright, 0.85f) : new Color(InkBlack, 0.5f),
            BorderColor = active ? BattlefieldBright : new Color(NoteBorder, 0.3f),
            BorderWidthBottom = active ? 2 : 0,
            BorderWidthLeft = 0,
            BorderWidthRight = 0,
            BorderWidthTop = 0,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };
        btn.AddThemeStyleboxOverride("normal", normal);

        var hover = new StyleBoxFlat
        {
            BgColor = active ? new Color(BloodBright, 0.9f) : new Color(InkBlack, 0.7f),
            BorderColor = active ? BattlefieldBright : new Color(NoteBorder, 0.5f),
            BorderWidthBottom = active ? 2 : 0,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeColorOverride("font_color", active ? NoteBg : NoteText);
        btn.AddThemeFontSizeOverride("font_size", 14);
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

    // ═══════════════════════════════════════════════
    // Tab: HUD
    // ═══════════════════════════════════════════════

    private void _BuildHudTab()
    {
        _AddSectionLabel("-- 顶部信息栏 --");
        _AddInfoCard("回合", "当前第几回合。每回合开始前会抽强化卡，回合越高敌人越强。", new Color(0.85f, 0.72f, 0.35f));
        _AddInfoCard("僵尸", "格式为 剩余/本回合总量。\n清完本回合僵尸后进入结算和下一次强化。", new Color(0.75f, 0.55f, 0.55f));
        _AddInfoCard("天气", "显示当前天气。带 ⚠ 的天气会造成危险效果，例如持续掉血或额外刷怪。", new Color(0.55f, 0.75f, 0.95f));
        _AddInfoCard("负面状态", "中毒、燃烧等持续掉血状态会在顶部显示，例如 ⚠ 中毒 -5/s。", new Color(0.9f, 0.35f, 0.85f));
        _AddInfoCard("分数", "击杀僵尸、精英和 BOSS 获得分数。BOSS 和精英给分更高。", new Color(0.95f, 0.55f, 0.45f));

        _AddSectionLabel("-- 底部战斗栏 --");
        _AddInfoCard("效果图标", "当前武器、装备、护盾、暴击、吸血等都会显示成图标。\n鼠标悬停图标可查看详细数值和说明。", new Color(0.85f, 0.75f, 0.45f));
        _AddInfoCard("生命", "生命值归零会死亡；低血量时血条会闪烁。\n回合结算时会恢复一部分最大生命。", new Color(0.35f, 0.75f, 0.45f));
        _AddInfoCard("闪避", "显示冲刺闪避是否就绪。就绪时按 Shift 冲刺，冷却时会显示剩余秒数。", new Color(0.45f, 0.7f, 1.0f));
        _AddInfoCard("武器 / 类型", "武器显示当前主要攻击卡；类型说明攻击方式：近战、远程、范围或魔法。", new Color(0.72f, 0.78f, 0.92f));
        _AddInfoCard("目标", "自动攻击优先锁定谁：最近、最远或密集敌群。\n按 Tab 可切换。", new Color(0.55f, 0.82f, 0.88f));
        _AddInfoCard("伤害 / 间隔", "伤害是单次攻击基础伤害；间隔是两次攻击之间的时间。\n间隔越短，攻击越快。", new Color(0.92f, 0.70f, 0.52f));
        _AddInfoCard("暴击 / 吸血 / 移速", "暴击提高爆发伤害；吸血会按伤害比例回血；移速影响走位和拉怪。", new Color(0.80f, 0.65f, 0.45f));

        _AddSectionLabel("-- 特殊系统提示 --");
        _AddInfoCard("力场", "左下角显示可用力场次数。\n按 ` 进入放置模式，左键放置，右键取消。⊕吸引、⊗排斥、⊙旋涡。", new Color(0.75f, 0.65f, 0.95f));
        _AddInfoCard("共振", "右上角共振指示器展示武器频率与尸潮波形的接近程度。\n越接近共振，部分伤害加成越明显。", new Color(0.45f, 0.85f, 0.95f));
        _AddInfoCard("装备面板", "按 C 或点击底部【装备】打开，查看身体部位槽位和每件装备的完整效果。", new Color(0.6f, 0.7f, 0.9f));
    }

    // ═══════════════════════════════════════════════
    // Tab: Controls
    // ═══════════════════════════════════════════════

    private void _BuildControlsTab()
    {
        _AddInfoCard("WASD / 方向键", "移动角色", new Color(0.3f, 0.8f, 0.3f));
        _AddInfoCard("空格 (按住)", "攻击\n魔法类自动锁定目标", new Color(0.8f, 0.3f, 0.3f));
        _AddInfoCard("Shift", "冲刺闪避，有冷却时间", new Color(0.4f, 0.7f, 1.0f));
        _AddInfoCard("Tab", "切换目标优先级 (最近/最远/密集)", new Color(0.5f, 0.8f, 0.9f));
        _AddInfoCard("ESC", "暂停/继续游戏\n强化阶段无法手动暂停", new Color(0.8f, 0.8f, 0.5f));
        _AddInfoCard("H", "打开/关闭帮助面板", new Color(0.7f, 0.6f, 0.8f));
        _AddInfoCard("C", "打开/关闭角色装备面板\n按身体部位查看已装备的武器/防具/饰品", new Color(0.6f, 0.7f, 0.9f));
        _AddInfoCard("1 / 2 / 3", "武器选择阶段快速选牌", new Color(0.6f, 0.7f, 0.4f));
        _AddInfoCard("Enter / Delete", "武器选择阶段跳过强化", new Color(0.7f, 0.6f, 0.5f));
    }

    // ═══════════════════════════════════════════════
    // Tab: Zombie Codex
    // ═══════════════════════════════════════════════

    private static readonly (string Name, string Desc, Color Color)[] ZombieEntries = new[]
    {
        ("基础僵尸", "最普通的僵尸，数量多但个体较弱", new Color(0.6f, 0.6f, 0.6f)),
        ("快速僵尸", "移动速度快，难以甩脱", new Color(0.3f, 0.8f, 0.3f)),
        ("坦克僵尸", "血量厚、伤害高，行动缓慢", new Color(0.5f, 0.5f, 0.7f)),
        ("爆炸僵尸", "死亡时爆炸，对周围造成范围伤害", new Color(0.9f, 0.5f, 0.2f)),
        ("隐身僵尸", "可以隐身，定期显形", new Color(0.4f, 0.6f, 0.8f)),
        ("喷毒僵尸", "远距离发射毒液弹，造成持续伤害", new Color(0.4f, 0.8f, 0.3f)),
        ("治疗僵尸", "会治愈附近的僵尸，应优先击杀", new Color(0.3f, 0.9f, 0.5f)),
        ("召唤师僵尸", "召唤小僵尸助战", new Color(0.7f, 0.4f, 0.8f)),
        ("射手僵尸", "远程射箭攻击", new Color(0.8f, 0.6f, 0.3f)),
        ("飞船僵尸", "空中单位，成群出现", new Color(0.5f, 0.6f, 0.9f)),
        ("机甲僵尸", "重型装甲，非常耐打", new Color(0.6f, 0.6f, 0.5f)),
        ("骑士僵尸", "攻守兼备的精英单位", new Color(0.8f, 0.7f, 0.3f)),
        ("飞龙僵尸", "强力空中单位，喷火攻击", new Color(0.9f, 0.3f, 0.2f)),
        ("虫群僵尸", "小型虫类，单体弱但数量多", new Color(0.5f, 0.7f, 0.3f)),
    };

    private void _BuildZombieTab()
    {
        _AddSectionLabel("-- 僵尸图鉴 --");

        foreach (var entry in ZombieEntries)
            _AddInfoCard(entry.Name, entry.Desc, entry.Color);

        _AddSectionLabel("-- 精英前缀 --");
        _AddInfoCard("再生", "精英僵尸缓慢回复生命值", new Color(0.3f, 0.9f, 0.4f));
        _AddInfoCard("迅捷", "精英僵尸移动速度大幅提升", new Color(0.3f, 0.7f, 1.0f));
        _AddInfoCard("爆炸", "精英僵尸死亡时产生爆炸", new Color(0.9f, 0.4f, 0.2f));

        _AddSectionLabel("-- BOSS --");
        _AddInfoCard("BOSS 僵尸", "每 5 回合出现一次\n体型巨大、属性强化为普通僵尸的数倍\n击杀后获得大量分数", new Color(0.9f, 0.2f, 0.2f));
    }

    // ═══════════════════════════════════════════════
    // Tab: Weapons
    // ═══════════════════════════════════════════════

    private void _BuildWeaponTab()
    {
        _AddSectionLabel("-- 武器类型 --");
        _AddInfoCard("近战", "扇形斩击攻击近距离敌人\n可搭配溅射、多目标、击退等效果", new Color(0.9f, 0.4f, 0.4f));
        _AddInfoCard("远程", "发射子弹打击远处敌人\n可搭配多射、穿透、轨迹伤害等", new Color(0.5f, 0.7f, 0.9f));
        _AddInfoCard("魔法", "发射追踪导弹，自动锁定敌人\n可搭配魅惑、推力、链式闪电等", new Color(0.7f, 0.5f, 0.9f));
        _AddInfoCard("范围", "爆炸范围伤害\n适合清理密集敌群", new Color(0.9f, 0.7f, 0.3f));

        _AddSectionLabel("-- 装备 --");
        _AddInfoCard("装备", "被动增强角色属性\n不会替换当前武器\n可与武器叠加装备", new Color(0.6f, 0.8f, 0.6f));

        _AddSectionLabel("-- 装备特殊效果 --");
        _AddInfoCard("防御类", "反伤: 受伤时有几率反弹伤害给攻击者\n闪避: 有概率完全躲避一次攻击\n减控: 缩短受到的负面效果持续时间", new Color(0.5f, 0.8f, 0.7f));
        _AddInfoCard("攻击类", "穿透: 子弹可贯穿更多敌人\n弹跳: 子弹反弹攻击多个目标\n溅射: 攻击波及周围敌人", new Color(0.85f, 0.6f, 0.4f));

        _AddSectionLabel("-- 武器特效 --");
        _AddInfoCard("近战特效", "溅射: 攻击波及周围敌人\n多目标: 同时攻击多个目标\n击杀爆炸: 击杀时产生爆炸\n击杀护盾: 击杀时获得护盾\n击退: 击退敌人", new Color(0.85f, 0.5f, 0.5f));
        _AddInfoCard("远程特效", "多射: 发射多枚子弹\n轨迹伤害: 子弹路径造成伤害\n回旋: 子弹可返回\n穿透: 子弹穿透敌人", new Color(0.5f, 0.65f, 0.85f));
        _AddInfoCard("魔法特效", "魅惑: 让敌人互相攻击\n推力: 击飞敌人\n眩晕: 短暂眩晕敌人\n链式: 伤害跳跃到附近敌人\n中毒: 持续伤害", new Color(0.65f, 0.5f, 0.85f));

        _AddSectionLabel("-- 品质与强化 --");
        _AddInfoCard("品质等级", "普通 (灰) < 稀有 (红) < 史诗 (金) < 传说\n高品质武器属性更强", new Color(0.85f, 0.75f, 0.5f));
        _AddInfoCard("强化卡", "选中已有装备的强化卡\n直接提升装备属性和特效等级\n可能附带副作用", new Color(0.8f, 0.6f, 0.3f));
    }

    // ═══════════════════════════════════════════════
    // Tab: Game Mechanics
    // ═══════════════════════════════════════════════

    private void _BuildMechanicsTab()
    {
        _AddSectionLabel("-- 回合流程 --");
        _AddInfoCard("回合循环", "每回合开始 → 选择武器卡牌 → 战斗 → 回合结算\n回合间恢复 20% 最大生命值", new Color(0.7f, 0.6f, 0.8f));

        _AddSectionLabel("-- 战斗机制 --");
        _AddInfoCard("连击系统", "连续击杀僵尸积累连击\n连击越高，伤害加成越大 (最高 +50%)\n停止击杀后连击会衰减", new Color(1.0f, 0.6f, 0.2f));
        _AddInfoCard("暴击", "暴击造成 2.5x 伤害\n暴击率可通过装备提升", new Color(0.9f, 0.5f, 0.7f));
        _AddInfoCard("吸血", "命中后回复伤害值一定比例的生命", new Color(0.7f, 0.3f, 0.3f));
        _AddInfoCard("护盾", "装备护盾领域后\n环绕的冰晶会伤害并减速接近的敌人", new Color(0.3f, 0.7f, 0.9f));

        _AddSectionLabel("-- 状态效果 --");
        _AddInfoCard("冰冻", "大幅降低敌人移动速度\n持续约 0.8 秒", new Color(0.4f, 0.8f, 1.0f));
        _AddInfoCard("中毒", "对敌人造成持续伤害\n持续约 3 秒", new Color(0.4f, 0.9f, 0.3f));
        _AddInfoCard("魅惑", "使敌人互相攻击\n持续一段时间", new Color(0.9f, 0.4f, 0.8f));
        _AddInfoCard("眩晕", "短暂使敌人无法行动", new Color(0.8f, 0.8f, 0.4f));

        _AddSectionLabel("-- 掉落物 --");
        _AddInfoCard("生命药水", "僵尸死亡后有概率掉落\n回复一定生命值", new Color(0.8f, 0.3f, 0.3f));
        _AddInfoCard("技能书", "僵尸死亡后有概率掉落\n精英和 BOSS 掉落概率更高", new Color(0.4f, 0.5f, 0.9f));

        _AddSectionLabel("-- 天气系统 --");
        _AddInfoCard("天气变化", "游戏中天气会随机变化\n暴风天气时可能额外生成僵尸", new Color(0.5f, 0.7f, 0.9f));

        _AddSectionLabel("-- 成就系统 --");
        _AddInfoCard("成就与里程碑", "完成特定目标解锁成就\n首次击杀、存活回合数、连击记录等\n成就会保存到存档中", new Color(0.9f, 0.8f, 0.3f));
    }

    // ═══════════════════════════════════════════════
    // UI Helpers
    // ═══════════════════════════════════════════════

    private void _AddSectionLabel(string text)
    {
        var spacer = new Control { CustomMinimumSize = new Vector2(0, 8) };
        _contentContainer.AddChild(spacer);

        var banner = new Panel();
        banner.CustomMinimumSize = new Vector2(0, 26);
        banner.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var bannerStyle = new StyleBoxFlat
        {
            BgColor = new Color(BloodBright, 0.15f),
            BorderWidthBottom = 2,
            BorderColor = new Color(BloodBright, 0.5f),
            ContentMarginLeft = 12,
            ContentMarginRight = 8,
            ContentMarginTop = 3,
            ContentMarginBottom = 3
        };
        banner.AddThemeStyleboxOverride("panel", bannerStyle);
        _contentContainer.AddChild(banner);

        var label = new Label
        {
            Text = "▎ " + text,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", BattlefieldBright);
        label.AddThemeConstantOverride("outline_size", 2);
        label.AddThemeColorOverride("font_outline_color", InkBlack);
        banner.AddChild(label);

        var spacer2 = new Control { CustomMinimumSize = new Vector2(0, 4) };
        _contentContainer.AddChild(spacer2);
    }

    private void _AddInfoCard(string title, string content, Color accentColor)
    {
        var cardStyle = new StyleBoxFlat
        {
            BgColor = new Color(NoteBg, 0.55f),
            BorderWidthLeft = 4,
            BorderColor = new Color(accentColor, 0.8f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 14,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };

        var card = new PanelContainer();
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        card.AddThemeStyleboxOverride("panel", cardStyle);
        _contentContainer.AddChild(card);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 3);
        card.AddChild(vbox);

        var titleLabel = new Label { Text = title };
        titleLabel.AddThemeFontSizeOverride("font_size", 14);
        titleLabel.AddThemeColorOverride("font_color", accentColor);
        titleLabel.AddThemeConstantOverride("outline_size", 1);
        titleLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.4f));
        vbox.AddChild(titleLabel);

        var contentLabel = new Label
        {
            Text = content,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        contentLabel.AddThemeFontSizeOverride("font_size", 12);
        contentLabel.AddThemeColorOverride("font_color", NoteText);
        vbox.AddChild(contentLabel);

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 3) };
        _contentContainer.AddChild(spacer);
    }
}
