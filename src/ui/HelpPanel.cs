namespace ZombieWars.UI;

using Godot;
using System.Collections.Generic;
using ZombieWars.Core;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>
/// Tabbed help panel — controls, zombie codex, weapon guide, and game mechanics.
/// Toggled via H key or the "?" HUD button.
/// </summary>
public partial class HelpPanel : Panel
{
    private int _currentTab;
    private readonly string[] _tabNames = { "操作", "僵尸", "武器", "机制" };
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
        card.CustomMinimumSize = new Vector2(620, 520);
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
            Text = "帮助面板",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        titleLabel.AddThemeColorOverride("font_color", BloodBright);
        titleLabel.AddThemeConstantOverride("outline_size", 2);
        titleLabel.AddThemeColorOverride("font_outline_color", InkBlack);
        titleRow.AddChild(titleLabel);

        var closeHint = new Label
        {
            Text = "按 H / ESC 关闭"
        };
        closeHint.AddThemeFontSizeOverride("font_size", 12);
        closeHint.AddThemeColorOverride("font_color", NoteText);
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
            BgColor = active ? new Color(NoteBorder, 0.8f) : new Color(NoteBg, 0.6f),
            BorderColor = active ? BloodBright : new Color(NoteBorder, 0.4f),
            BorderWidthBottom = active ? 3 : 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", normal);
        btn.AddThemeColorOverride("font_color", active ? NoteBg : NoteText);
        btn.AddThemeFontSizeOverride("font_size", 14);
    }

    private void _BuildContent()
    {
        foreach (var child in _contentContainer.GetChildren())
            child.QueueFree();

        switch (_currentTab)
        {
            case 0: _BuildControlsTab(); break;
            case 1: _BuildZombieTab(); break;
            case 2: _BuildWeaponTab(); break;
            case 3: _BuildMechanicsTab(); break;
        }
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
        var spacer = new Control { CustomMinimumSize = new Vector2(0, 6) };
        _contentContainer.AddChild(spacer);

        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", NoteText);
        _contentContainer.AddChild(label);

        var spacer2 = new Control { CustomMinimumSize = new Vector2(0, 2) };
        _contentContainer.AddChild(spacer2);
    }

    private void _AddInfoCard(string title, string content, Color accentColor)
    {
        var cardStyle = new StyleBoxFlat
        {
            BgColor = new Color(NoteBg, 0.75f),
            BorderWidthLeft = 3,
            BorderColor = new Color(accentColor, 0.7f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };

        var card = new PanelContainer();
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        card.AddThemeStyleboxOverride("panel", cardStyle);
        _contentContainer.AddChild(card);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 10);
        hbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        card.AddChild(hbox);

        var titleLabel = new Label
        {
            Text = title,
            CustomMinimumSize = new Vector2(120, 0)
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 13);
        titleLabel.AddThemeColorOverride("font_color", accentColor);
        titleLabel.AddThemeConstantOverride("outline_size", 1);
        titleLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.3f));
        hbox.AddChild(titleLabel);

        var contentLabel = new Label
        {
            Text = content,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        contentLabel.AddThemeFontSizeOverride("font_size", 12);
        contentLabel.AddThemeColorOverride("font_color", NoteText);
        hbox.AddChild(contentLabel);

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 2) };
        _contentContainer.AddChild(spacer);
    }
}
