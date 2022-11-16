using Sculptures.AI.Traits;
using Sculptures.Components;
using System;
using System.Collections.Generic;
using Game.AI;
using Game.AI.Actions;
using Game.AI.Needs;
using Game.Components;
using Game.Constants;
using Game.Data;
using Game.Utils;
using KL.Grid;
using KL.Utils;
using UnityEngine;

namespace Sculptures.AI.Actions{
	public sealed class ActSculpt : ActionExecutor{
		public enum Phase{
			FindEquipment,
			ReachEquipment,
			UseEquipment,
			FinishSculpting
		}

		public const string ActType = "Sculpt";

		public static readonly int TypeHash = Animator.StringToHash(ActType);

		private Phase phase;

		private ISculptingProvider seekedEquipment;

		private HashSet<int> ignoredEquipment;

		private Slot slot;

		private int navTarget;

		private Need restNeed;

		private Need showerNeed;

		private Need hungerNeed;

		private Need funNeed;

		private Need stressNeed;

		private Need purposeNeed;

		private float restRateBefore;

		private float showerRateBefore;

		private float hungerRateBefore;

		private long sculptingSince;

		private bool sculpting = false;

		public override bool IsTiring => true;

		public Phase CurrentPhase => phase;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Register() {
			ActionExecutor.RegisterExecutor("Sculpt", (Advert ad, Being being) => new ActSculpt(ad, being));
		}

		public ActSculpt(Advert ad, Being worker): base(ad, worker) {
		}

		public override void Cleanup() {
			D.Warn("Sculpt cleanup for " + worker);
			if (seekedEquipment != null)
			{
				seekedEquipment.EndSculpting(worker);
				seekedEquipment = null;
			}
			if (restNeed != null)
			{
				restNeed.DropRateMod = restRateBefore;
				restNeed = null;
			}
			if (showerNeed != null)
			{
				showerNeed.DropRateMod = showerRateBefore;
				showerNeed = null;
			}
			if (hungerNeed != null)
			{
				hungerNeed.DropRateMod = hungerRateBefore;
				hungerNeed = null;
			}
			UnreserveSlot();
			if (sculpting && sculptingSince > 0)
			{
				long val = S.Ticks - sculptingSince;
				val = Math.Max(val, MoodEffect.Duration1h);
				worker.Mood.AddEffect(MoodEffect.Create(S.Ticks, val, "sculptures.created_art".T(), 6));
			}
			sculpting = false;
		}

		protected override void OnLoad(ComponentData data) {
			seekedEquipment = data.GetComponent<ISculptingProvider>(S, "SeekedEquipment");
			sculptingSince = data.GetLong("SculptingSince", sculptingSince);
			int @int = data.GetInt("SlotIdx", 0);
			if (@int != 0)
			{
				slot = S.Sys.Slots.Get(@int);
				ReserveSlot();
			}
			navTarget = data.GetInt("NavTarget", 0);
			if (navTarget != 0)
			{
				subAction = ActMoveToPos.SubActionTo(navTarget, worker, updateAnchor: true, groundedSoftFail: false);
			}
			phase = (Phase)data.GetInt("Phase", 0);
			if (phase == Phase.UseEquipment)
			{
				phase = Phase.ReachEquipment;
			}
		}

		public override void OnSave() {
			ComponentData vars = ad.Vars;
			vars.SetInt("Phase", (int)phase);
			vars.SetLong("SculptingSince", sculptingSince);
			vars.SetComponent("SeekedEquipment", seekedEquipment);
			if (slot != null)
			{
				vars.SetInt("SlotIdx", slot.SlotPosIdx);
			}
			vars.SetInt("NavTarget", navTarget);
		}

		private void UnreserveSlot() {
			D.Warn("Unreserve slot for " + worker);
			if (slot != null) {
				D.Warn("Slot is not null");
				slot.Unreserve(worker);
				D.Warn("Slot unreserved");
				if (slot.ContainedBeing != null) {
					slot.Remove(worker);
				}
				slot = null;
			}
		}

		private bool ReserveSlot() {
			D.Warn("Reserve slot for " + worker);
			if (slot == null) {
				D.Warn("Slot is null");
				return false;
			}
			if (!slot.Reserve(worker)) {
				D.Warn("Could not reserve slot for " + worker);
				slot = null;
				return false;
			}
			return true;
		}

		protected override ExecutionResult DoWork() {
			D.Warn("Do sculpting work; subaction: " + subAction + "; phase: " + phase + "; worker: " + worker);
			if (subAction != null) {
				ExecutionResult result = subAction.Execute();
				if (!result.IsFinished) {
					return result;
				}
				if (!result.IsSuccess && seekedEquipment != null) {
					IgnoreEquipment(seekedEquipment);
				}
				subAction = null;
				navTarget = 0;
			}
			switch (phase) {
				case Phase.FindEquipment:
					return FindEquipment();
				case Phase.ReachEquipment:
					return ReachEquipment();
				case Phase.UseEquipment:
					return UseEquipment();
				case Phase.FinishSculpting:
					return FinishSculpting();
				default:
					D.Err("Unhandled phase: {0}", phase);
					return Failure(T.AdRejUndefined);
			}
		}

		private ExecutionResult FindEquipment() {
			D.Warn("Find equipment for " + worker);
			slot = S.Sys.Slots.FindForDesignation<ISculptingProvider>(worker, SculpturesMod.SlotDesignationSculpting, ignoredEquipment, out var obj);
			D.Warn("Find slot: " + slot);
			if (slot == null) {
				return Failure("sculpting.adrejected.lackequipment".T());
			}
			if (ReserveSlot()) {
				phase = Phase.ReachEquipment;
				seekedEquipment = obj;
				navTarget = obj.WorkSpot;
				subAction = ActMoveToPos.SubActionTo(navTarget, worker, updateAnchor: true, groundedSoftFail: false);
				return StillWorking;
			}
			IgnoreEquipment(obj);
			return StillWorking;
		}

		private ExecutionResult ReachEquipment()
		{
			if (!IsNearEquipment()) {
				IgnoreEquipment(seekedEquipment);
				phase = Phase.FindEquipment;
				return StillWorking;
			}
			if (!slot.Put(worker)) {
				return Failure(T.AdRejTargetUnavailable);
			}
			funNeed = worker.Needs.GetNeed(NeedId.Fun);
			stressNeed = worker.Needs.GetNeed(NeedId.Stress);
			purposeNeed = worker.Needs.GetNeed(NeedId.Purpose);
			showerNeed = worker.Needs.GetNeed(NeedId.Shower, warn: false);
			if (showerNeed != null) {
				showerRateBefore = showerNeed.DropRateMod;
				showerNeed.DropRateMod = 1.5f;
			}
			restNeed = worker.Needs.GetNeed(NeedId.Rest, warn: false);
			if (restNeed != null) {
				restRateBefore = restNeed.DropRateMod;
				restNeed.DropRateMod = 0.5f;
			}
			hungerNeed = worker.Needs.GetNeed(NeedId.Hunger, warn: false);
			if (hungerNeed != null) {
				hungerRateBefore = hungerNeed.DropRateMod;
				hungerNeed.DropRateMod = 1.25f;
			}
			seekedEquipment.BeginSculpting(worker);
			if (sculptingSince == 0L) {
				sculptingSince = S.Ticks;
			}
			phase = Phase.UseEquipment;
			return StillWorking;
		}

		private ExecutionResult UseEquipment() {
			ISculptingProvider sculptingProvider = seekedEquipment;
			if (sculptingProvider == null || !sculptingProvider.Entity.IsActive) {
				return Failure(T.AdRejTargetUnavailable);
			}
			if (!seekedEquipment.Sculpt(worker)) {
				phase = Phase.FinishSculpting;
			}
			else {
				sculpting = true;
				// Fun+, stress+, purpose+
				if (S.Rng.Chance(0.5f)) {
					stressNeed.Add(0.1f);
				}
				funNeed.Add(0.1f);
				purposeNeed.Add(worker.Traits.HasTrait(TraitCreative.Id) ? 0.1f : 0.01f);
				long num = S.Ticks - sculptingSince;
				// 540 == 3 hours, 1440 == 8 hours
				if (num > 540 && (num > 1440 || (funNeed.Value > 99f && stressNeed.Value > 99f))) {
					phase = Phase.FinishSculpting;
				} else {
					worker.Skills.Advance(ad.SkillChecks);
				}
			}
			return StillWorking;
		}

		private void IgnoreEquipment(ISculptingProvider e) {
			UnreserveSlot();
			(ignoredEquipment ?? (ignoredEquipment = new HashSet<int>())).Add(e.EntityId);
		}

		private bool IsNearEquipment() {
			if (seekedEquipment == null)
			{
				return false;
			}
			return Pos.AreAdjacent(seekedEquipment.WorkSpot, worker.PosIdx);
		}

		private ExecutionResult FinishSculpting() {
			return Success;
		}
	}
}
