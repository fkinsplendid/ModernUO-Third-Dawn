/* SkillBallPlus.cs v2.3.2 (Complete rewrite) by Ixtabay
Based on original SkillBall from Romanthebrain
Updates by Hawthornetr, ntony, JamzeMcC, MrNice, Ixtabay, and others since 2010
Updated for ModernUO by ImaNewb
*/

using Server.Network;
using Server.Gumps;
using System.Collections.Generic;

namespace Server
{
    public class SkillPickGump : DynamicGump
    {
        public static int skillsToBoost = 3;  // How many skills to boost
        private SkillBallPlus m_SkillBallPlus;
        private Mobile _player;
        public static double boostValue = 100;  // How high to boost each selected skill
        public string blueSix = "<BASEFONT SIZE=6 FACE=1 COLOR=#001052>";
        public string blueEight = "<BASEFONT SIZE=8 FACE=1 COLOR=#001052>";
        public string blueTen = "<BASEFONT SIZE=10 FACE=1 COLOR=#001052>";
        public string brownEight = "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>";
        public string endFont = "</BASEFONT>";

        public SkillPickGump(SkillBallPlus ball, Mobile player) : base(0, 0)
        {
            m_SkillBallPlus = ball;
            _player = player;
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            // Background and header/footer
            builder.AddBackground(39, 33, 555, 545, 9380);
            builder.AddHtml(67, 41, 1153, 20, "<BASEFONT SIZE=10 FACE=1 COLOR=#001052> 3 x GM Skillball</BASEFONT>".ToCharArray());
            builder.AddHtml(67, 555, 1153, 20, $"<BASEFONT SIZE=10 FACE=1 COLOR=#001052>Please select {skillsToBoost} skills to raise to {boostValue}</BASEFONT>".ToCharArray());
            builder.AddButton(420, 555, 2071, 2072, (int)Buttons.Close);
            builder.AddButton(490, 555, 2311, 2312, (int)Buttons.FinishButton);

            // Miscellaneous Section
            builder.AddImage(64, 68, 2086);
            builder.AddCheckbox(65, 85, 2510, 2511, false, (int)SkillName.ArmsLore);
            builder.AddCheckbox(65, 105, 2510, 2511, false, (int)SkillName.Begging);
            builder.AddCheckbox(65, 125, 2510, 2511, false, (int)SkillName.Camping);
            builder.AddCheckbox(65, 145, 2510, 2511, false, (int)SkillName.Cartography);
            builder.AddCheckbox(65, 165, 2510, 2511, false, (int)SkillName.Forensics);
            builder.AddCheckbox(65, 185, 2510, 2511, false, (int)SkillName.ItemID);
            builder.AddCheckbox(65, 205, 2510, 2511, false, (int)SkillName.TasteID);
            builder.AddHtml(85, 65, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#001052>Miscellaneous</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 85, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Arms Lore</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 105, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Begging</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 125, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Camping</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 145, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Cartography</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 165, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Forensic Evaluation</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 185, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Item Identification</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 205, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Taste Identification</BASEFONT>".ToCharArray());

            // Combat Section
            builder.AddImage(64, 228, 2086);
            builder.AddCheckbox(65, 245, 2510, 2511, false, (int)SkillName.Anatomy);
            if (_player.Race != Race.Gargoyle) builder.AddCheckbox(65, 265, 2510, 2511, false, (int)SkillName.Archery);
            builder.AddCheckbox(65, 285, 2510, 2511, false, (int)SkillName.Fencing);
            if (Core.AOS) builder.AddCheckbox(65, 305, 2510, 2511, false, (int)SkillName.Focus);
            builder.AddCheckbox(65, 325, 2510, 2511, false, (int)SkillName.Healing);
            builder.AddCheckbox(65, 345, 2510, 2511, false, (int)SkillName.Macing);
            builder.AddCheckbox(65, 365, 2510, 2511, false, (int)SkillName.Parry);
            builder.AddCheckbox(65, 385, 2510, 2511, false, (int)SkillName.Swords);
            builder.AddCheckbox(65, 405, 2510, 2511, false, (int)SkillName.Tactics);
            if (Core.SA && _player.Race == Race.Gargoyle) builder.AddCheckbox(65, 425, 2510, 2511, false, (int)SkillName.Throwing);
            builder.AddCheckbox(65, 445, 2510, 2511, false, (int)SkillName.Wrestling);
            builder.AddHtml(85, 225, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#001052>Combat</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 245, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Anatomy</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 265, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Archery</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 285, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Fencing</BASEFONT>".ToCharArray());
            //builder.AddHtml(85, 305, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Focus</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 325, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Healing</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 345, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Mace Fighting</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 365, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Parrying</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 385, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Swordfighting</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 405, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Tactics</BASEFONT>".ToCharArray());
            //builder.AddHtml(85, 425, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Throwing</BASEFONT>".ToCharArray());
            builder.AddHtml(85, 445, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Wrestling</BASEFONT>".ToCharArray());

            // Debug info for Throwing visibility
            // builder.AddHtml(100, 600, 400, 20, $"Throwing: {(int)SkillName.Throwing}, Race: {_player.Race}, SA: {Core.SA}".ToCharArray());

            // Trade Skills Section
            builder.AddImage(239, 68, 2086);
            builder.AddCheckbox(240, 85, 2510, 2511, false, (int)SkillName.Alchemy);
            builder.AddCheckbox(240, 105, 2510, 2511, false, (int)SkillName.Blacksmith);
            builder.AddCheckbox(240, 125, 2510, 2511, false, (int)SkillName.Fletching);
            builder.AddCheckbox(240, 145, 2510, 2511, false, (int)SkillName.Carpentry);
            builder.AddCheckbox(240, 165, 2510, 2511, false, (int)SkillName.Cooking);
            builder.AddCheckbox(240, 185, 2510, 2511, false, (int)SkillName.Inscribe);
            builder.AddCheckbox(240, 205, 2510, 2511, false, (int)SkillName.Lumberjacking);
            builder.AddCheckbox(240, 225, 2510, 2511, false, (int)SkillName.Mining);
            builder.AddCheckbox(240, 245, 2510, 2511, false, (int)SkillName.Tailoring);
            builder.AddCheckbox(240, 265, 2510, 2511, false, (int)SkillName.Tinkering);
            builder.AddHtml(259, 65, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#001052>Trade Skills</BASEFONT>".ToCharArray());
            builder.AddHtml(260, 85, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Alchemy</BASEFONT>".ToCharArray());
            builder.AddHtml(260, 105, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Blacksmithy</BASEFONT>".ToCharArray());
            builder.AddHtml(260, 125, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Bowcraft/Fletching</BASEFONT>".ToCharArray());
            builder.AddHtml(260, 145, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Carpentry</BASEFONT>".ToCharArray());
            builder.AddHtml(260, 165, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Cooking</BASEFONT>".ToCharArray());
            builder.AddHtml(260, 185, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Inscription</BASEFONT>".ToCharArray());
            builder.AddHtml(260, 205, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Lumberjacking</BASEFONT>".ToCharArray());
            builder.AddHtml(260, 225, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Mining</BASEFONT>".ToCharArray());
            builder.AddHtml(260, 245, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Tailoring</BASEFONT>".ToCharArray());
            builder.AddHtml(259, 265, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Tinkering</BASEFONT>".ToCharArray());

            // Magic Section
            builder.AddImage(239, 288, 2086);
            if (Core.SE) builder.AddCheckbox(240, 305, 2510, 2511, false, (int)SkillName.Bushido);
            if (Core.AOS) builder.AddCheckbox(240, 325, 2510, 2511, false, (int)SkillName.Chivalry);
            builder.AddCheckbox(240, 345, 2510, 2511, false, (int)SkillName.EvalInt);
            if (Core.SA) builder.AddCheckbox(240, 365, 2510, 2511, false, (int)SkillName.Imbuing);
            builder.AddCheckbox(240, 385, 2510, 2511, false, (int)SkillName.Magery);
            builder.AddCheckbox(240, 405, 2510, 2511, false, (int)SkillName.Meditation);
            if (Core.SA) builder.AddCheckbox(240, 425, 2510, 2511, false, (int)SkillName.Mysticism);
            if (Core.AOS) builder.AddCheckbox(240, 445, 2510, 2511, false, (int)SkillName.Necromancy);
            if (Core.SE) builder.AddCheckbox(240, 465, 2510, 2511, false, (int)SkillName.Ninjitsu);
            builder.AddCheckbox(240, 485, 2510, 2511, false, (int)SkillName.MagicResist);
            if (Core.ML) builder.AddCheckbox(240, 505, 2510, 2511, false, (int)SkillName.Spellweaving);
            builder.AddCheckbox(240, 525, 2510, 2511, false, (int)SkillName.SpiritSpeak);
            builder.AddHtml(260, 285, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#001052>Magic</BASEFONT>".ToCharArray());
            //builder.AddHtml(260, 305, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Bushido</BASEFONT>".ToCharArray());
            //builder.AddHtml(260, 325, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Chivalry</BASEFONT>".ToCharArray());
            builder.AddHtml(260, 345, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Evaluating Intelligence</BASEFONT>".ToCharArray());
            //builder.AddHtml(260, 365, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Imbuing</BASEFONT>".ToCharArray());
            builder.AddHtml(260, 385, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Magery</BASEFONT>".ToCharArray());
            builder.AddHtml(260, 405, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Meditation</BASEFONT>".ToCharArray());
            //builder.AddHtml(260, 425, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Mysticism</BASEFONT>".ToCharArray());
            //builder.AddHtml(260, 445, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Necromancy</BASEFONT>".ToCharArray());
            //builder.AddHtml(260, 465, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Ninjitsu</BASEFONT>".ToCharArray());
            builder.AddHtml(260, 485, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Resisting Spells</BASEFONT>".ToCharArray());
            //builder.AddHtml(260, 505, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Spellweaving</BASEFONT>".ToCharArray());
            builder.AddHtml(260, 525, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Spirit Speak</BASEFONT>".ToCharArray());

            // Wilderness Section
            builder.AddImage(429, 68, 2086);
            builder.AddCheckbox(430, 85, 2510, 2511, false, (int)SkillName.AnimalLore);
            builder.AddCheckbox(430, 105, 2510, 2511, false, (int)SkillName.AnimalTaming);
            builder.AddCheckbox(430, 125, 2510, 2511, false, (int)SkillName.Fishing);
            builder.AddCheckbox(430, 145, 2510, 2511, false, (int)SkillName.Herding);
            builder.AddCheckbox(430, 165, 2510, 2511, false, (int)SkillName.Tracking);
            builder.AddCheckbox(430, 185, 2510, 2511, false, (int)SkillName.Veterinary);
            builder.AddHtml(450, 65, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#001052>Wilderness</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 85, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Animal Lore</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 105, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Animal Taming</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 125, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Fishing</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 145, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Herding</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 165, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Tracking</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 185, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Veterinary</BASEFONT>".ToCharArray());

            // Thieving Section
            builder.AddImage(429, 208, 2086);
            builder.AddCheckbox(430, 225, 2510, 2511, false, (int)SkillName.DetectHidden);
            builder.AddCheckbox(430, 245, 2510, 2511, false, (int)SkillName.Hiding);
            builder.AddCheckbox(430, 265, 2510, 2511, false, (int)SkillName.Lockpicking);
            builder.AddCheckbox(430, 285, 2510, 2511, false, (int)SkillName.Poisoning);
            builder.AddCheckbox(430, 305, 2510, 2511, false, (int)SkillName.RemoveTrap);
            builder.AddCheckbox(430, 325, 2510, 2511, false, (int)SkillName.Snooping);
            builder.AddCheckbox(430, 345, 2510, 2511, false, (int)SkillName.Stealing);
            builder.AddCheckbox(430, 365, 2510, 2511, false, (int)SkillName.Stealth);
            builder.AddHtml(450, 205, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#001052>Thieving</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 225, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Detect Hidden</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 245, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Hiding</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 265, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Lockpicking</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 285, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Poisoning</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 305, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Remove Trap</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 325, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Snooping</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 345, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Stealing</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 365, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Stealth</BASEFONT>".ToCharArray());

            // Bard Section
            builder.AddImage(429, 388, 2086);
            builder.AddCheckbox(430, 405, 2510, 2511, false, (int)SkillName.Discordance);
            builder.AddCheckbox(430, 425, 2510, 2511, false, (int)SkillName.Musicianship);
            builder.AddCheckbox(430, 445, 2510, 2511, false, (int)SkillName.Peacemaking);
            builder.AddCheckbox(430, 465, 2510, 2511, false, (int)SkillName.Provocation);
            builder.AddHtml(450, 385, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#001052>Bard</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 405, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Discordance</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 425, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Musicianship</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 445, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Peacemaking</BASEFONT>".ToCharArray());
            builder.AddHtml(450, 465, 2314, 20, "<BASEFONT SIZE=8 FACE=1 COLOR=#5a4a31>Provocation</BASEFONT>".ToCharArray());
        }

        public enum Buttons
        {
            Close,
            FinishButton,
        }

        public override void OnResponse(NetState state, in RelayInfo info)
        {
            Mobile m = state.Mobile;
            m.SendMessage("SkillBall OnResponse fired!");

            if (info.ButtonID != (int)Buttons.FinishButton)
                return;

            if (info.Switches.Length < skillsToBoost)
            {
                m.SendGump(new SkillPickGump(m_SkillBallPlus, m));
                m.SendMessage($"Please try again.  You must pick {skillsToBoost - info.Switches.Length} more skills for a total of {skillsToBoost}.");
                return;
            }
            else if (info.Switches.Length > skillsToBoost)
            {
                m.SendGump(new SkillPickGump(m_SkillBallPlus, m));
                m.SendMessage($"Please try again.  You chose {info.Switches.Length - skillsToBoost} more skills than the {skillsToBoost} allowed.");
                return;
            }

            // Collect selected skills
            var selectedSkills = new List<SkillName>();
            foreach (int idx in info.Switches)
                selectedSkills.Add((SkillName)idx);

            // Set all skills to 0
            foreach (Skill skill in m.Skills)
                skill.Base = 0;

            // Set selected skills to boostValue
            foreach (SkillName skillName in selectedSkills)
                m.Skills[skillName].Base = boostValue;

            // Set skill cap
            m.SkillsCap = (int)(skillsToBoost * boostValue);

            // Optionally: give items for selected skills here

            m_SkillBallPlus.Delete();
            m.Delta(MobileDelta.Stat);
        }

    }

    public class SkillBallPlus : Item
    {
        private int m_skillsToBoost = SkillPickGump.skillsToBoost; // Default number of skills to boost
        private double m_boostValue = SkillPickGump.boostValue; // Default level skills will be boosted to
        private string m_BaseName = "a Skill Ball";

        [CommandProperty(AccessLevel.GameMaster)]
        public int skillsToBoost
        {
            get { return m_skillsToBoost; }
            set
            {
                m_skillsToBoost = value;
            }
        }
        [CommandProperty(AccessLevel.GameMaster)]
        public double boostValue
        {
            get { return m_boostValue; }
            set
            {
                m_boostValue = value;
            }
        }

        public override string DefaultName
        {
            get
            {
                return m_BaseName;
            }
        }

        [Constructible]
        public SkillBallPlus() : base(0xE73)
        {
            Weight = 1.0;
            Hue = 1153;
            Movable = true;
            LootType = LootType.Newbied;
        }
        public override void OnDoubleClick(Mobile m)
        {

            if (m.Backpack != null && m.Backpack.GetAmount(typeof(SkillBallPlus)) > 0)
            {
                m.SendMessage("Please choose " + SkillPickGump.skillsToBoost + " skills to set to " + SkillPickGump.boostValue + ".");
                GumpSystem.CloseGump<SkillPickGump>(m);
                m.SendGump(new SkillPickGump(this, m));
            }
            else
                m.SendMessage(" This must be in your backpack to function.");

        }

        public SkillBallPlus(Serial serial) : base(serial)
        { }

        public override void Serialize(IGenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(IGenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
        }

    }
}
