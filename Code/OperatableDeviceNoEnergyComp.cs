using System.Collections.Generic;
using Game.AI;
using Game.Constants;
using Game.Data;
using Game.Utils;
using UnityEngine;

namespace Game.Components
{
	public sealed class OperatableDeviceNoEnergyComp : BaseComponent<OperatableDeviceNoEnergyComp>, IUIDataProvider, IAdvertProvider, IAdvertPriorityListener
	{
		public bool IsAutomated;

		private string jobType;

		private string jobIcon;

		private string jobDescriptionKey;

		private string jobActionKey;

		private string promiseType;

		private int promiseTypeHash;

		private int promiseAmount;

		private string rewardType;

		private int rewardTypeHash;

		private int rewardAmount;

		private string _jobDescriptionT;

		private string _jobActionT;

		private string requiredSkill;

		private int requiredSkillHash;

		private int requiredSkillLevel;

		public bool CanBeOperated;

		public Being Operator;

		private Vector2[] offsets;

		public string AnimationType;

		private UDB dataBlock;

		private Advert ad;

		private int adPriority;

		private string jobDescriptionT => _jobDescriptionT ?? (_jobDescriptionT = jobDescriptionKey.T());

		public string JobActionT => _jobActionT ?? (_jobActionT = jobActionKey.T());

		private Vector2 offset => offsets[(int)Tile.Transform.Rotation];

		public Vector2 OperatorPosition => Tile.Position + offset;

		public Facing.Type OperatorFacing => Facing.Opposite(Tile.Transform.Rotation);

		public bool HasCancellableAd => Advert.IsActive(ad);

		public Advert CurrentAdvert => ad;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Register()
		{
			BaseComponent<OperatableDeviceNoEnergyComp>.AddComponentPrototype(new OperatableDeviceNoEnergyComp());
		}

		public void HailOperator()
		{
			CanBeOperated = true;
			if (!HasCancellableAd)
			{
				ad = CreateAdvert(jobType, jobIcon, jobDescriptionT).MarkAsWork().WithPriority(adPriority).WalkToWorkSpot(1)
					.AndThen("OperateDevice", jobDescriptionT, rewardTypeHash, rewardAmount)
					.WithSkillCheck(requiredSkillHash, requiredSkillLevel)
					.Publish();
				if (promiseType != null)
				{
					ad.WithPromise(promiseTypeHash, promiseAmount);
				}
			}
		}

		public void DismissOperator()
		{
			CanBeOperated = false;
			Advert advert = ad;
			if (advert != null && advert.WorkerEntityId == 0)
			{
				ad.NotReturnable("Dismissing operator");
				CancelAdvert("Dismissing operator");
			}
		}

		protected override void OnConfig()
		{
			requiredSkill = base.Config.GetString("RequiredSkill", "Operate");
			requiredSkillHash = Animator.StringToHash(requiredSkill);
			requiredSkillLevel = base.Config.GetInt("RequiredSkillLevel", 1);
			rewardType = base.Config.GetString("RewardType", "Purpose");
			rewardTypeHash = Animator.StringToHash(rewardType);
			rewardAmount = base.Config.GetInt("RewardAmount", 5);
			promiseType = base.Config.GetString("PromiseType", null);
			if (promiseType != null)
			{
				promiseTypeHash = Animator.StringToHash(promiseType);
			}
			promiseAmount = base.Config.GetInt("PromiseAmount", 10);
			jobDescriptionKey = base.Config.GetString("JobDescriptionKey", "operate.device");
			jobActionKey = base.Config.GetString("JobActionKey", "act.OperateDevice");
			jobType = base.Config.GetString("JobType", "Operations");
			jobIcon = base.Config.GetString("JobIcon", "Icons/Color/Operate");
			offsets = base.Config.GetVector2Set("OperatorOffsets");
			adPriority = base.Config.GetInt("TaskPriority", 5);
			AnimationType = base.Config.GetString("AnimationType", "Punch");
		}

		public override void OnSave()
		{
			GetOrCreateData().SetInt("AdPriority", adPriority);
		}

		protected override void OnLoad(ComponentData data)
		{
			adPriority = data.GetInt("AdPriority", 5);
		}

		public override string ToString()
		{
			return $" * OperatableDevice [{requiredSkill}/{requiredSkillLevel}]";
		}

		public UDB GetUIBlock()
		{
			if (IsAutomated || !Tile.IsConstructed)
			{
				return null;
			}
			if (dataBlock == null)
			{
				dataBlock = UDB.Create(this, UDBT.IText, "Icons/Color/PreviewHuman01", T.OperatorRequired);
			}
			return dataBlock;
		}

		public override void Receive(IComponent sender, int message)
		{
		}

		public void CancelAdvert(string why)
		{
			if (HasCancellableAd)
			{
				S.Adverts.Cancel(ad, why);
			}
		}

		public void OnAdvertLoad(Advert ad)
		{
			this.ad = ad;
		}

		public void OnAdvertCancelled(Advert ad)
		{
			if (!ad.IsReturnable)
			{
				this.ad = null;
			}
		}

		public void OnAdvertComplete(Advert ad)
		{
			this.ad = null;
		}

		public void OnAdvertPriorityChange(int priority)
		{
			adPriority = priority;
		}

		public void GetUIDetails(List<UDB> res)
		{
		}
	}
}