using Server.Mobiles;

namespace Server.Engines.Companions;

public abstract partial class BaseCompanion
{
    /// <summary>
    /// The smith has handed a piece back (already in the pack). Put it straight back on if the
    /// slot is free; otherwise the next gear check decides, as for anything else in the pack.
    /// </summary>
    public void OnRepairReturned(Item item, RepairOutcome outcome)
    {
        if (!IsDeadPet && item.Layer is not (Layer.Invalid or Layer.Backpack) && FindItemOnLayer(item.Layer) == null)
        {
            EquipItem(item);
        }

        GearDirty = true;

        switch (outcome)
        {
            case RepairOutcome.Repaired:
                {
                    Speak(SpeechContext.Repaired, MasterBlacksmith.GetNoun(item), urgent: true);
                    break;
                }
            case RepairOutcome.Declined:
                {
                    Speak(SpeechContext.RepairDeclined, MasterBlacksmith.GetNoun(item), urgent: true);
                    break;
                }
        }

        (AIObject as CompanionAI)?.OnRepairReturned(outcome);
    }
}
