using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.TownLife;

/// <summary>
/// Someone who lives in a town: a name, a trade, a front door and a manner. Their day follows
/// the game clock (see <see cref="ResidentSchedule"/>): to work, to market, to the tavern, home.
/// They walk where anyone can see them; out of everyone's sight they simply get there. At home
/// they are indoors, off the map, until their day starts again.
/// </summary>
[SerializationGenerator(0)]
public partial class Resident : BaseCreature
{
    [SerializableField(0, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private ResidentTrade _trade;

    [SerializableField(1, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _townName;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Point3D _home;

    [SerializableField(3, setter: "private")]
    private bool _indoors;

    [SerializableField(4, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private ResidentManner _manner;

    // Where they're headed and why. Worked out afresh after a restart.
    private DayPhase _phase = DayPhase.Count;
    private PlaceKind _goalKind;
    private TownPlace _goalPlace;
    private Point3D _goal;
    private bool _hasGoal;
    private long _dwellUntil;
    private List<Point3D> _route;
    private int _routeIndex;

    [Constructible]
    public Resident() : this(ResidentTrade.Townsfolk, null)
    {
    }

    public Resident(ResidentTrade trade, Town town) : base(AIType.AI_Use_Default, FightMode.None, 10, 1)
    {
        _trade = trade;
        _townName = town?.Name;
        _manner = (ResidentManner)Utility.Random((int)ResidentManner.Count);

        ActiveSpeed = 0.2;
        PassiveSpeed = 0.4;
        CurrentSpeed = 0.4;

        Hue = Race.Human.RandomSkinHue();

        if (Female = trade != ResidentTrade.Watchman && Utility.RandomBool())
        {
            Body = 0x191;
            Name = NameList.RandomName("female");
        }
        else
        {
            Body = 0x190;
            Name = NameList.RandomName("male");
        }

        Title = TitleFor(trade);

        SetStr(45, 70);
        SetDex(45, 70);
        SetInt(35, 60);
        SetHits(40, 60);

        if (trade == ResidentTrade.Watchman)
        {
            SetSkill(SkillName.Swords, 50.0, 65.0);
            SetSkill(SkillName.Tactics, 50.0, 65.0);
        }

        Karma = 1000;

        Utility.AssignRandomHair(this);

        if (!Female && Utility.RandomBool())
        {
            Utility.AssignRandomFacialHair(this, HairHue);
        }

        Dress();
        PackGold(5, 30);
    }

    public override bool CanTeach => false;

    public override bool ClickTitle => true;

    public override bool AlwaysMurderer => false;

    public override bool BardImmune => false;

    public override bool PlayerRangeSensitive => true;

    public Town Town => TownLifeSystem.FindTown(_townName);

    public bool IsTravelling => _hasGoal && _route != null && _routeIndex < _route.Count;

    public bool HasArrived => _hasGoal && !IsTravelling;

    public PlaceKind GoalKind => _goalKind;

    public TownPlace GoalPlace => _goalPlace;

    public Point3D Goal => _goal;

    public DayPhase Phase => _phase;

    private ResidentAI _ai;

    // One AI for life; the engine asks for it more than once.
    protected override BaseAI ForcedAI => _ai ??= new ResidentAI(this);

    public static string TitleFor(ResidentTrade trade) =>
        trade switch
        {
            ResidentTrade.Farmer    => "the farmer",
            ResidentTrade.Fisher    => "the fisher",
            ResidentTrade.Smith     => "the smith's apprentice",
            ResidentTrade.Shopper   => "the townsperson",
            ResidentTrade.Drunk     => "the sot",
            ResidentTrade.Minstrel  => "the minstrel",
            ResidentTrade.Watchman  => "of the night watch",
            ResidentTrade.Beggar    => "the beggar",
            ResidentTrade.Courier   => "the courier",
            ResidentTrade.Noble     => "the noble",
            ResidentTrade.Lurker    => Utility.RandomList("the idler", "the hawker", "the gambler", "the dandy", "the layabout"),
            _                       => "the townsperson"
        };

    // ---------- The day ----------

    /// <summary>
    /// Works out where they ought to be right now and puts them there directly: for newcomers,
    /// after a restart, or when nobody is looking.
    /// </summary>
    public void TakeUpRoutine()
    {
        PlanNext(true);
        SkipToGoal();
    }

    /// <summary>
    /// Called regularly whether or not anyone is near. Starts a new part of the day when the
    /// clock moves on, or somewhere new when they've stayed long enough. Out of sight, they're
    /// moved straight there rather than walking.
    /// </summary>
    public void CheckRoutine(bool watched)
    {
        var town = Town;

        if (town?.IsReady != true || Deleted || !Alive)
        {
            return;
        }

        var newPhase = town.Phase != _phase;

        if (newPhase || !_hasGoal || HasArrived && Core.TickCount - _dwellUntil >= 0)
        {
            PlanNext(newPhase);
        }

        if (_indoors)
        {
            if (_goalKind != PlaceKind.Home)
            {
                StepOutside();
            }

            return;
        }

        if (!watched && (IsTravelling || _goalKind == PlaceKind.Home && HasArrived))
        {
            SkipToGoal();
        }
    }

    /// <summary>Chooses the next place for this part of the day and plans the walk there.</summary>
    public void PlanNext(bool newPhase)
    {
        var town = Town;

        if (town?.IsReady != true)
        {
            return;
        }

        _phase = town.Phase;

        var kind = ResidentSchedule.Choose(_trade, _phase, town.Has);

        if (kind == PlaceKind.Home && _home == Point3D.Zero)
        {
            kind = PlaceKind.Street;
        }

        if (!town.TryPickSpot(this, kind, out var spot, out var place) &&
            !town.TryPickSpot(this, kind = PlaceKind.Street, out spot, out place))
        {
            _hasGoal = false;
            return;
        }

        var (min, max) = ResidentSchedule.DwellMinutes(_trade);
        _dwellUntil = Core.TickCount + Utility.RandomMinMax(min, max) * 60000L;

        _goalKind = kind;
        _goalPlace = place;
        _goal = spot;
        _hasGoal = true;

        _route = Map == null || Map == Map.Internal ? null : town.Streets.FindRoute(Location, spot);
        _routeIndex = 0;
    }

    /// <summary>The next waypoint on the way, or false once there.</summary>
    public bool TryGetWaypoint(out Point3D waypoint)
    {
        if (_route == null || _routeIndex >= _route.Count)
        {
            waypoint = default;
            return false;
        }

        waypoint = _route[_routeIndex];
        return true;
    }

    public void ReachedWaypoint()
    {
        _routeIndex++;

        if (!IsTravelling)
        {
            OnArrived();
        }
    }

    /// <summary>Lost the way (someone in the road, a closed gate): plan it again from here.</summary>
    public void Reroute()
    {
        var town = Town;

        if (town?.IsReady == true && _hasGoal)
        {
            _route = town.Streets.FindRoute(Location, _goal);
            _routeIndex = 0;

            if (_route.Count == 0)
            {
                // No way there from here at all; somewhere else will do.
                PlanNext(false);
            }
        }
    }

    private void OnArrived()
    {
        _route = null;

        if (_goalKind == PlaceKind.Home)
        {
            GoIndoors();
        }
    }

    /// <summary>Puts them at their goal at once. Home means indoors.</summary>
    private void SkipToGoal()
    {
        var town = Town;

        if (!_hasGoal || town == null)
        {
            return;
        }

        _route = null;
        _routeIndex = 0;

        if (_goalKind == PlaceKind.Home)
        {
            GoIndoors();
            return;
        }

        _indoors = false;
        MoveToWorld(_goal, town.Map);
    }

    private void GoIndoors()
    {
        if (_indoors)
        {
            return;
        }

        if (Map != null && Map != Map.Internal)
        {
            Effects.PlaySound(Location, Map, 0xEA);
        }

        Combatant = null;
        Warmode = false;
        _indoors = true;
        Internalize();
    }

    private void StepOutside()
    {
        var town = Town;

        if (town == null || _home == Point3D.Zero)
        {
            return;
        }

        _indoors = false;
        MoveToWorld(_home, town.Map);
        Effects.PlaySound(_home, town.Map, 0xEA);

        _route = town.Streets.FindRoute(_home, _goal);
        _routeIndex = 0;
    }

    // ---------- Looks ----------

    private void Dress()
    {
        var neutral = Utility.RandomNeutralHue();

        switch (_trade)
        {
            case ResidentTrade.Farmer:
                {
                    AddItem(Utility.RandomBool() ? new StrawHat() : new WideBrimHat(Utility.RandomNeutralHue()));
                    AddItem(new Shirt(Utility.RandomNeutralHue()));
                    AddItem(new LongPants(neutral));
                    AddItem(new Boots());
                    AddItem(new Pitchfork());
                    break;
                }
            case ResidentTrade.Fisher:
                {
                    AddItem(new FloppyHat(Utility.RandomNeutralHue()));
                    AddItem(new Shirt(Utility.RandomNeutralHue()));
                    AddItem(new ShortPants(neutral));
                    AddItem(new Sandals());
                    AddItem(new FishingPole());
                    break;
                }
            case ResidentTrade.Smith:
                {
                    AddItem(new FullApron(Utility.RandomNeutralHue()));
                    AddItem(new Shirt(Utility.RandomNeutralHue()));
                    AddItem(new LongPants(neutral));
                    AddItem(new Boots());
                    AddItem(new SmithHammer());
                    break;
                }
            case ResidentTrade.Drunk:
                {
                    AddItem(new Shirt(Utility.RandomNeutralHue()));
                    AddItem(new ShortPants(neutral));
                    break;
                }
            case ResidentTrade.Minstrel:
                {
                    AddItem(new FeatheredHat(Utility.RandomBrightHue()));
                    AddItem(new FancyShirt(Utility.RandomBrightHue()));
                    AddItem(new Doublet(Utility.RandomBrightHue()));
                    AddItem(new LongPants(neutral));
                    AddItem(new Boots());
                    PackItem(new Lute());
                    break;
                }
            case ResidentTrade.Watchman:
                {
                    AddItem(new LeatherChest());
                    AddItem(new Cloak(Utility.RandomNondyedHue()));
                    AddItem(new LongPants(neutral));
                    AddItem(new Boots());
                    AddItem(new Lantern());
                    break;
                }
            case ResidentTrade.Beggar:
                {
                    AddItem(new Robe(Utility.RandomNeutralHue()));
                    AddItem(new Sandals());
                    break;
                }
            case ResidentTrade.Courier:
                {
                    AddItem(new Tunic(Utility.RandomBrightHue()));
                    AddItem(new Shirt(Utility.RandomNeutralHue()));
                    AddItem(new ShortPants(neutral));
                    AddItem(new Boots());
                    break;
                }
            case ResidentTrade.Noble:
                {
                    var rich = Utility.RandomBrightHue();

                    if (Female)
                    {
                        AddItem(new FancyDress(rich));
                    }
                    else
                    {
                        AddItem(new FancyShirt(rich));
                        AddItem(new Doublet(Utility.RandomBrightHue()));
                        AddItem(new LongPants(neutral));
                    }

                    AddItem(new Cloak(rich));
                    AddItem(new Boots());
                    PackGold(100, 200);
                    break;
                }
            case ResidentTrade.Lurker:
                {
                    // A mixed crowd: would-be adventurers, show-offs in their finery, robed dabblers.
                    switch (Utility.Random(3))
                    {
                        case 0:
                            {
                                AddItem(new LeatherChest());
                                AddItem(new LeatherLegs());
                                AddItem(new Boots());
                                break;
                            }
                        case 1:
                            {
                                AddItem(new FancyShirt(Utility.RandomBrightHue()));
                                AddItem(new Doublet(Utility.RandomBrightHue()));
                                AddItem(new LongPants(neutral));
                                AddItem(new Cloak(Utility.RandomBrightHue()));
                                AddItem(new ThighBoots());
                                break;
                            }
                        default:
                            {
                                AddItem(new Robe(Utility.RandomBrightHue()));
                                AddItem(new WizardsHat(Utility.RandomBrightHue()));
                                AddItem(new Sandals());
                                break;
                            }
                    }

                    PackGold(20, 80);
                    break;
                }
            default:
                {
                    if (Female)
                    {
                        AddItem(Utility.RandomBool() ? new PlainDress(Utility.RandomNeutralHue()) : new FancyDress(Utility.RandomNeutralHue()));
                        AddItem(new Shoes(Utility.RandomNeutralHue()));
                    }
                    else
                    {
                        AddItem(new Shirt(Utility.RandomNeutralHue()));
                        AddItem(new LongPants(neutral));
                        AddItem(new Shoes(Utility.RandomNeutralHue()));
                    }

                    if (_trade == ResidentTrade.Shopper)
                    {
                        AddItem(new HalfApron(Utility.RandomNeutralHue()));
                    }

                    break;
                }
        }
    }

    // ---------- Lifetime ----------

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        // A street-mapping probe left over from a build the server didn't finish.
        if (_townName == null && Map == Map.Internal)
        {
            Delete();
            return;
        }

        Town?.Register(this);
    }

    public override void OnDelete()
    {
        Town?.Unregister(this);
        base.OnDelete();
    }

    public override void OnAfterDelete()
    {
        _goalPlace = null;
        _route = null;
        base.OnAfterDelete();
    }
}
