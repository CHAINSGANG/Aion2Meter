using System;

namespace AionFlex.Core.Models;

public static class FeatureKeys
{
	public const string PartyOverlay = "party_overlay";

	public const string SessionHistory = "session_history";

	public const string PersonalBests = "personal_bests";

	public const string Training = "training";

	public const string AccuracyAnalytics = "accuracy_analytics";

	public const string DefenseAnalytics = "defense_analytics";

	public const string DamageTaken = "damage_taken";

	public const string SkillRotation = "skill_rotation";

	public const string CrossSession = "cross_session";

	public const string PerBossHistory = "per_boss_history";

	public const string Checklist = "checklist";

	public const string DataUploadToggle = "data_upload_toggle";

	public const string UpdateChannelLatest = "update_channel_latest";

	public const string PlayerDetails = "player_details";

	public const string PvpMode = "pvp_mode";

	public const string MechanicsAlerts = "mechanics_alerts";

	public const string OverlayAppearance = "overlay_appearance";

	public const string EventAlerts = "event_alerts";

	public const string LootEnergy = "loot_energy";

	public const string GearInspect = "gear_inspect";

	public static string For(Feature feature)
	{
		return feature switch
		{
			Feature.PartyOverlay => "party_overlay", 
			Feature.SessionHistory => "session_history", 
			Feature.PersonalBests => "personal_bests", 
			Feature.Training => "training", 
			Feature.AccuracyAnalytics => "accuracy_analytics", 
			Feature.DefenseAnalytics => "defense_analytics", 
			Feature.DamageTaken => "damage_taken", 
			Feature.SkillRotation => "skill_rotation", 
			Feature.CrossSession => "cross_session", 
			Feature.PerBossHistory => "per_boss_history", 
			Feature.Checklist => "checklist", 
			Feature.DataUploadToggle => "data_upload_toggle", 
			Feature.UpdateChannelLatest => "update_channel_latest", 
			Feature.PlayerDetails => "player_details", 
			Feature.PvpMode => "pvp_mode", 
			Feature.MechanicsAlerts => "mechanics_alerts", 
			Feature.OverlayAppearance => "overlay_appearance", 
			Feature.EventAlerts => "event_alerts", 
			Feature.LootEnergy => "loot_energy", 
			Feature.GearInspect => "gear_inspect", 
			_ => throw new ArgumentOutOfRangeException("feature", feature, "Unmapped Feature constant — add a case in FeatureKeys.For."), 
		};
	}
}
