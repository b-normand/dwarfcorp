using System.Linq;
using System;

namespace DwarfCorp
{
    public static partial class ActHelper
    {
        public static Act CreateToolCheckAct(CreatureAI Agent, bool AllowHands, params String[] ToolType)
        {
            if (Agent.Creature.Equipment.HasValue(out var equipment))
                return new Sequence(
                    // Unequip an un-matching tool.
                    //new Not(new Domain(() =>
                    //    {
                    //        if (equipment.GetItemInSlot("tool").HasValue(out var tool) && tool.ResourceType.HasValue(out var res))
                    //            return !res.Tags.Any(t => ToolType.Contains(t));
                    //        return true;
                    //    },
                    //    new UnequipToolAct(Agent))),
                    new Select(
                        new Condition(() =>
                        {
                            if (!Agent.Stats.CurrentClass.RequiresTools) return true;

                            // If hands are allowed, we do not already have a tool, and no tool is available - use hands.
                            if (AllowHands && !equipment.GetItemInSlot("tool").HasValue())
                                if (!Agent.World.GetFirstStockpileContainingResourceWithMatchingTag(ToolType.ToList()).HasValue)
                                    return true;

                            if (equipment.GetItemInSlot("tool").HasValue(out var tool) && tool.ResourceType.HasValue(out var res))
                                return res.Tags.Any(t => ToolType.Contains(t));

                            return false;
                        }),
                        new Sequence(
                            new GetAnySingleMatchingResourceAct(Agent, ToolType.ToList()) { BlackboardEntry = "tool-stashed" },
                            new EquipToolAct(Agent) { BlackboardEntry = "tool-stashed" }
                        )));
            else
            {
                if (Agent.Stats.CurrentClass.RequiresTools)
                    return new Condition(() => false);
                else
                    return new Condition(() => true);
            }
        }
    }
}