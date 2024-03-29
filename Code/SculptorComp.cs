using Sculptures.AI.Traits;
using Sculptures.Constants;
using Sculptures.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game;
using Game.AI;
using Game.Commands;
using Game.Components;
using Game.Constants;
using Game.Data;
using Game.Input;
using Game.Research;
using Game.UI;
using Game.Utils;
using KL.Collections;
using KL.Grid;
using KL.Randomness;
using KL.Utils;
using UnityEngine;

namespace Sculptures.Components
{
	public sealed class SculptorComp : BaseSlotsComp<SculptorComp>, IUIDataProvider, IComponent, IAdvertProvider, ICopyableComp, IAdvertPriorityListener, IUIContextMenuProvider, IRelocatable, IAfterInitDef, IMatRequester, ISculptingProvider, ISlots, IUISubmenuProvider, ICanBeIdle
	{
		private int level;

		public CraftingDemand Demand;

		private ExtraInfoComp extraInfo;

		private MatDeficitEventHolder deficitEv;

		public float Progress;

		private string uiItemIcon;

		private string craftKey;

		private int craftPriority;

		private string progressBarKey;

		public int TotalProduced;

		private float craftingSpeedMultiplier;

		private bool isIdleWarningsOn;

		private bool isStorageFullWarningsOn;

		public HashSet<string> CompatibleTypes;

		private UDB priorityBlock;

		private UDB idleWarnBlock;

		private UDB storageFullBlock;

		private Mat[] ingredients;

		private List<Craftable> allCraftables;

		private static readonly Dictionary<MatType, int> availableMaterials = new Dictionary<MatType, int>();

		private readonly SculptorComp[] similarCrafters = new SculptorComp[3];

		private Advert currentAd;

		private UDB demandBlock;

		private readonly Dictionary<MatType, UDB> ingredientBlocks = new Dictionary<MatType, UDB>();

		private ColorChoice colChoice;

		private UDB prodBlock;

		private UDB prodCurBlock;

		private bool showingUnconfiguredIcon;

		private bool showingStorageFullIcon;

		private bool isShowingIdle;

		public string IdleCategory => Tile.Definition.NameT;

		public Tile DeviceTile => Tile;

		public Def ProductDef => Demand?.Product;

		public string IdleTooltip => $"{T.InfoProdGoalReached}<br>{Demand.AmountWanted}<br>{T.Owned}: {S.Sys.Inventory.CountOf(Demand.Product)}";

		private bool isSculpting = false;

		private Vector2[] offsets;

		private Vector2[] sculptureStorageOffsets;

		private EventNotification storageFullNotification;

		private string storageFullGroupId;

		private string storageFullTitle;

		private Tile producedSculpture;

		private float averageEfficiency;

		private float startedSculpting;

		private List<int> sculptorIds;
		
		private List<string> sculptorNames;

		public bool IsConsumerActive => true;

		public float MaxConsumption
		{
			get
			{
				if (Demand == null)
				{
					return 0f;
				}
				return Demand.Craftable.EnergyCost;
			}
		}

		public bool IsCopyable => Tile.ENode.IsReachable;

		public string CopyConfigText => Tile.Definition.NameT;

		public bool HasCancellableAd => Advert.IsActive(currentAd);

		public Advert CurrentAdvert => currentAd;

		public int MatReqPos => Tile.Transform.WorkSpotOrPos;

		public List<MatRequest> MissingMats { get; } = new List<MatRequest>();

		public override int SlotDesignation => SculpturesMod.SlotDesignationSculpting;

		public int WorkSpot => base.Tile.Transform.WorkSpot;

		public override Facing.Type SpotRotation => Facing.Opposite(Tile.Transform.Rotation);

		private static readonly StringBuilder tooltipSB = new StringBuilder();

		public override bool HasSubmenuNow => true;

		private string SculptingTitle = "Sculpting title";

		public override string SubmenuTitle => SculptingTitle;

		public override bool IsAvailable
		{
			get
			{
				if (Tile.IsConstructed)
				{
					return !Tile.IsPendingRemoval && Demand != null && !IsMissingMaterials() && !showingStorageFullIcon;
				}
				return false;
			}
		}

		public MatRequest NextMissingMat
		{
			get
			{
				if (Arrays.IsEmpty(MissingMats))
				{
					return null;
				}
				for (int i = 0; i < MissingMats.Count; i++)
				{
					MatRequest matRequest = MissingMats[i];
					if (matRequest.Amount > 0)
					{
						return matRequest;
					}
				}
				return null;
			}
		}

		public string DeviceCategory => T.MenuProduction;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Register() {
			BaseComponent<SculptorComp>.AddComponentPrototype(new SculptorComp());
		}

		protected override void OnConfig() {
			deficitEv = new MatDeficitEventHolder(Tile, this, DeviceCategory);
			level = base.Config.GetInt("Level");
			craftPriority = base.Config.GetInt("DefaultPriority", 5);
			isIdleWarningsOn = base.Config.GetBool("IdleWarnDefault", def: true);
			isStorageFullWarningsOn = base.Config.GetBool("StorageFullWarnDefault", def: true);
			CompatibleTypes = new HashSet<string>(base.Config.GetStringSet("CompatibleTypes"));
			string @string = base.Config.GetString("UIItemIcon", null);
			craftingSpeedMultiplier = base.Config.GetFloat("SpeedMultiplier", 1f);
			if (string.IsNullOrWhiteSpace(@string))
			{
				uiItemIcon = Tile.Definition.Preview;
			}
			else
			{
				uiItemIcon = @string;
			}
			progressBarKey = base.Config.GetString("ProgressBarKey", "printing.item");
			craftKey = base.Config.GetString("CraftKey", "print");
			offsets = base.Config.GetVector2Set("OperatorOffsets");
			sculptureStorageOffsets = base.Config.GetVector2Set("SculptureStorageOffsets");
			OnConfigSlots();
		}

		public override void OnSave() {
			ComponentData orCreateData = GetOrCreateData();
			orCreateData.SetFloat("Progress", Progress);
			orCreateData.SetInt("Priority", craftPriority);
			orCreateData.SetInt("TotalProd", TotalProduced);
			orCreateData.SetBool("StorageFullWarn", isStorageFullWarningsOn);
			orCreateData.SetBool("IdleWarn", isIdleWarningsOn);
			CraftingDemand.Save(Demand, orCreateData);
			if (Demand != null)
			{
				deficitEv.Save(orCreateData);
			}
			orCreateData.SetMats("Ingredients", ingredients);
			if (producedSculpture != null) {
				// Save produced sculpture because of full storage
				orCreateData.SetString("ProducedSculpture", producedSculpture.Definition.Id);
			}
			orCreateData.SetFloat("AverageEfficiency", averageEfficiency);
			orCreateData.SetFloat("StartedSculpting", startedSculpting);
			int[] scIds = null;
			if (sculptorIds != null) {
				scIds = sculptorIds.ToArray();
			}
			orCreateData.SetIntSet("SculptorIds", scIds);
			string[] scNames = null;
			if (sculptorNames != null) {
				scNames = sculptorNames.ToArray();
			}
			orCreateData.SetStringSet("SculptorNames", scNames);
		}

		protected override void OnLoad(ComponentData data)
		{
			SculptingTitle = "sculptor_comp.title".T();
			Demand = CraftingDemand.Load(data);
			if (Demand != null)
			{
				deficitEv.Load(data);
				ingredients = data.GetMats("Ingredients", null);
				if (ingredients == null)
				{
					SetIngredients();
				}
				else
				{
					VerifyIngredients();
				}
			}
			isIdleWarningsOn = data.GetBool("IdleWarn", isIdleWarningsOn);
			isStorageFullWarningsOn = data.GetBool("StorageFullWarn", isStorageFullWarningsOn);
			craftPriority = data.GetInt("Priority", craftPriority);
			TotalProduced = data.GetInt("TotalProd", 0);
			Progress = data.GetFloat("Progress", 0f);
			string producedSculptureDefId = data.GetString("ProducedSculpture", null);
			if (producedSculptureDefId != null) {
				Def def = The.Defs.TryGet(producedSculptureDefId);
				producedSculpture = EntityUtils.CreatePrototype<Tile>(def);
			}
			averageEfficiency = data.GetFloat("AverageEfficiency", 0);
			startedSculpting = data.GetFloat("StartedSculpting", 0);
			int[] sculptorIdsData = data.GetIntSet("SculptorIds", null);
			if (sculptorIdsData != null) {
				sculptorIds = new List<int>(sculptorIdsData);
			}
			string[] sculptorNamesData = data.GetStringSet("SculptorNames", null);
			if (sculptorNamesData != null) {
				sculptorNames = new List<string>(sculptorNamesData);
			}
		}

		private void VerifyIngredients()
		{
			Mat[] array = Demand.Craftable.Ingredients;
			bool flag = false;
			if (array.Length != ingredients.Length)
			{
				flag = true;
				D.ErrSoft("Incorrect ingredients count for sculptor {0} recipe {1}: {2} != {3}", base.Entity.DefinitionId, Demand.Craftable.Id, ingredients.Length, array.Length);
			}
			else
			{
				for (int i = 0; i < array.Length; i++)
				{
					if (array[i].Type != ingredients[i].Type)
					{
						D.ErrSoft("Incorrect ingredient for sculptor {0} recipe {1}: {2} != {3}", base.Entity.DefinitionId, Demand.Craftable.Id, array[i].Type, ingredients[i].Type);
						flag = true;
						break;
					}
				}
			}
			if (flag)
			{
				SetIngredients();
			}
		}

		private void SetIngredients()
		{
			if (Demand != null)
			{
				ingredients = IngredientsFor(Demand.Craftable);
			}
		}

		public override string ToString()
		{
			return $" * Sculptor [Level {level}]";
		}

		public new UDB GetUIBlock()
		{
			if (!Tile.IsConstructed || !Tile.ENode.IsReachable)
			{
				return null;
			}
			if (dataBlock == null)
			{
				dataBlock = UDB.Create(this, UDBT.IText, null).WithGroupId(UDBGH.Management);
			}
			UpdateUIBlock(wasUpdated: false);
			return dataBlock;
		}

		private void UpdateUIBlock(bool wasUpdated)
		{
			UpdateProgressBar();
			if (wasUpdated)
			{
				UDB uDB = dataBlock;
				if (uDB == null || !uDB.IsShowing)
				{
					return;
				}
			}
			if (dataBlock != null)
			{
				if (Demand == null)
				{
					dataBlock.UpdateIcon("Icons/Color/Warning");
					dataBlock.UpdateTitle(T.InfoDeviceUnconfigured);
					dataBlock.UpdateTooltip(null);
				}
				else
				{
					dataBlock.UpdateIcon(uiItemIcon);
					dataBlock.UpdateTitle(progressBarKey.T(Demand.CraftableItemName));
					dataBlock.UpdateTooltip(DefTooltip(Demand.Craftable.ProductDef));
				}
			}
			prodBlock?.UpdateText(Units.XNum(TotalProduced));
			if (Demand == null || demandBlock == null || (!demandBlock.IsShowing && wasUpdated)) {
				return;
			}
			prodCurBlock.UpdateText(Units.XNum(Demand.AmountProducedThis));
			demandBlock.UpdateValue(Progress);
			Mat[] array = ingredients;
			for (int i = 0; i < array.Length; i++) {
				Mat mat = array[i];
				UDB uDB2 = null;
				if (ingredientBlocks.TryGetValue(mat.Type, out var value)) {
					uDB2 = value;
				}
				if (uDB2 == null) {
					// D.Err("Updating up block mismatch ingredients. Expected: {0}, Actual: {1}", D.Dump(ingredients), D.Dump(ingredientBlocks));
				}
				else {
					uDB2.UpdateValue(mat.StackSize);
				}
			}
		}

		public override bool IsCompatibleWith(Being being) {
			return being.Persona.Species.IsHumanoid && being.Skills.GetSkill(SculpturesMod.SkillIdArtistic, false) != null;
		}

		public override void OnLateReady(bool wasLoaded) {
			extraInfo = base.Entity.GetRequiredComponent<ExtraInfoComp>(this);
			if (wasLoaded)
			{
				deficitEv.OnLateReady();
				UpdateExtraInfo();
			}
			CreateBeingSlots("Icons/Color/Sculpt", "sculpting.slots".T());
		}

		private void UpdateExtraInfo() {
			if (Demand != null)
			{
				extraInfo?.ShowInfoIcon(Demand.Product.Preview, Demand.Product.NameT);
			}
			else
			{
				extraInfo?.HideInfoIcon();
			}
		}

		public override void OnRemove() {
			RemoveSlots();
		}

		private void UpdateProgressBar() {
			if (Demand != null && Progress > 0f)
			{
				ShowInfoBar(Game.Constants.Consts.ColorFuelBar, Progress, Progress, 1, progressBarKey.T(Units.Percentage(Progress))).IsPretranslated = true;
			}
			else
			{
				HideInfoBar();
			}
		}

		private List<Craftable> GetCraftableItems() {
			if (allCraftables == null)
			{
				allCraftables = new List<Craftable>();
				Craftable.ForCrafter(CompatibleTypes, allCraftables);
			}
			return allCraftables;
		}

		private void LoadCraftingOptions(List<UDB> blocks) {
			availableMaterials.Clear();
			S.Sys.Inventory.LoadRemainingMaterials(availableMaterials);
			List<Craftable> craftableItems = GetCraftableItems();
			int num = 0;
			ResearchTree research = S.Research;
			foreach (Craftable c in craftableItems)
			{
				Def item = c.ProductDef;
				if (research.IsDefUnlocked(item))
				{
					num++;
					UDB b = UDB.Create(this, UDBT.ITextBtn, item.Preview, item.NameT);
					b.WithIconTint(item.UITint);
					b.WithIconClickFunction(item.ShowManualEntry);
					b.WithTooltipFunction((UDB _) => ShowProductionChoiceTooltipFor(c, item));
					b.WithText2(craftKey.T());
					b.WithClickFunction(delegate
					{
						SwitchToCrafting(c);
						b.NeedsListRebuild = true;
						UpdateUIBlock(wasUpdated: true);
					});
					blocks.Add(b);
				}
			}
			if (num == 0)
			{
				blocks.Add(UDB.Create(this, UDBT.DText, "Icons/Color/Info", T.NoBlueprints).WithIconClickFunction(delegate
				{
					UIPopupWidget.Spawn("Icons/Color/Info", T.NoBlueprints, T.NoBlueprintsHelp);
				}));
			}
		}

		private string ShowProductionChoiceTooltipFor(Craftable c, Def item) {
			Caches.StringBuilder.Clear();
			Caches.StringBuilder.Append("available.count".T(SculptureQualities.GetVariationsCount(S, item)));
			Caches.StringBuilder.AppendFormat("<br>{0}: <b>{1}@{2}</b>", T.ProductionCost, Units.SecondsToShortTime(c.ProductionTimeHours * 3600f), Units.Electricity(c.EnergyCost));
			Mat[] array = IngredientsFor(c);
			Caches.StringBuilder.AppendFormat("<br>{0}", T.Ingredients);
			Mat[] array2 = array;
			for (int i = 0; i < array2.Length; i++) {
				Mat mat = array2[i];
				Caches.StringBuilder.AppendFormat("<br> - <b>{0}</b> [{1}]", mat.Type.NameT, Units.XNum(mat.MaxStackSize));
			}
			if (c.OutputMultiplier > 1) {
				Caches.StringBuilder.AppendFormat("<br>{0}: <b>{1}</b>", T.OutputMultiplier, Units.XNum(c.OutputMultiplier));
			}
			return Caches.StringBuilder.ToString();
		}

		public void SwitchToCrafting(Craftable craftable) {
			if (showingUnconfiguredIcon) {
				showingUnconfiguredIcon = false;
				HideInfoIcon();
			}
			demandBlock = null;
			Demand = CraftingDemand.CreateFor(craftable);
			Demand.Type = CraftingDemandType.Unlimited;
			ingredients = IngredientsFor(craftable);
			Progress = 0f;
			extraInfo.ShowInfoIcon(Demand.Product.Preview, Demand.Product.NameT);
			CheckMissingIngredients(checkIfProducedEnough: true);
			UpdateExtraInfo();
		}

		private Mat[] IngredientsFor(Craftable c) {
			Mat[] array = c.Ingredients;
			Mat[] array2 = new Mat[array.Length];
			for (int i = 0; i < array.Length; i++) {
				array2[i] = new Mat {
					Type = array[i].Type,
					MaxStackSize = array[i].StackSize
				};
			}
			return array2;
		}

		private void BuildIngredientBlocks(List<UDB> blocks) {
			ingredientBlocks.Clear();
			Mat[] array = ingredients;
			for (int i = 0; i < array.Length; i++) {
				Mat mat = array[i];
				UDB uDB = mat.DataBlock(S, T.Ingredient);
				ingredientBlocks[mat.Type] = uDB;
				blocks.Add(uDB);
			}
		}

		private string DemandTooltip(UDB b) {
			return DefTooltip(Demand.Product);
		}

		private string DefTooltip(Def def) {
			tooltipSB.Clear();
			tooltipSB.AppendFormat("<b>{0}</b><br>", "sculptures.created.sculpture".T());
			tooltipSB.Append("available.count".T(SculptureQualities.GetVariationsCount(S, def)));
			tooltipSB.AppendFormat("<br><br><b>{0}</b><br>{1}", def.NameT, def.ExtendedDesc);
			return tooltipSB.ToString();
		}

		private void LoadDemandOptions(List<UDB> blocks) {
			if (prodCurBlock == null) {
				prodCurBlock = UDB.Create(this, UDBT.DText, "Icons/Color/Count", T.CurrentItemsProduced);
			}
			prodCurBlock.UpdateText(Units.XNum(Demand.AmountProducedThis));
			blocks.Add(prodCurBlock);
			if (demandBlock == null) {
				Def product = Demand.Product;
				demandBlock = UDB.Create(this, UDBT.IProgress, product.Preview, product.NameT).WithTooltipFunction(DemandTooltip).WithIconClickFunction(product.ShowManualEntry)
					.AsPercentage()
					.WithIconTint(product.UITint);
				int outputMultiplier = Demand.Craftable.OutputMultiplier;
				if (outputMultiplier > 1) {
					demandBlock.WithText(Units.XNum(outputMultiplier));
				}
			}
			blocks.Add(demandBlock);
			BuildIngredientBlocks(blocks);
			BuildColorBlock(blocks);
			if (priorityBlock == null) {
				priorityBlock = UDB.Create(this, UDBT.IPriority, "Icons/White/Priority", T.TaskPriority).WithRange(0f, 9f).WithValueOf(craftPriority)
					.WithClickFunction(delegate
					{
						MovePriority(-1);
					})
					.WithClick2Function(delegate
					{
						MovePriority(1);
					})
					.WithIconClickFunction(delegate
					{
						UIPopupWidget.Spawn("Icons/White/Priority", T.HaulPriority, T.HaulPriorityHelp);
					});
			}
			blocks.Add(priorityBlock);
			if (idleWarnBlock == null)
			{
				idleWarnBlock = UDB.Create(this, UDBT.DTextBtn, null, T.DeviceWarnIfIdle).WithText2(T.Toggle).WithClickFunction(ToggleDeviceIdle);
			}
			UpdateDeviceIdleBlock();
			blocks.Add(idleWarnBlock);
			if (storageFullBlock == null) {
				storageFullBlock = UDB.Create(this, UDBT.DTextBtn, null, "sculpturebench.warn_if_storage_full".T()).WithText2(T.Toggle).WithClickFunction(ToggleShowStorageFull);
			}
			UpdateStorageFullBlock();
			blocks.Add(storageFullBlock);
			blocks.Add(UDB.Create(this, UDBT.DBtn, "Icons/Color/Cross", T.StopProducing).WithClickFunction(delegate {
				StopProducing();
				UpdateUIBlock(wasUpdated: true);
			}));
			UpdateUIBlock(wasUpdated: false);
		}

		private void MovePriority(int direction) {
			craftPriority = UIUtils.AdjustPriority0To9(craftPriority, direction);
			if (HasCancellableAd) {
				CurrentAdvert.WithPriority(craftPriority);
			}
			priorityBlock.UpdateValue(craftPriority);
		}

		private void UpdateDeviceIdleBlock()
		{
			if (isIdleWarningsOn)
			{
				idleWarnBlock.UpdateIcon("Icons/Color/Check");
				idleWarnBlock.UpdateText(T.On);
			}
			else
			{
				idleWarnBlock.UpdateIcon("Icons/Color/Cross");
				idleWarnBlock.UpdateText(T.Off);
			}
		}

		private void ToggleDeviceIdle()
		{
			if (isIdleWarningsOn)
			{
				isIdleWarningsOn = false;
				HideIdleNotification();
			}
			else
			{
				isIdleWarningsOn = true;
			}
			UpdateDeviceIdleBlock();
		}

		private void UpdateStorageFullBlock()
		{
			if (isStorageFullWarningsOn)
			{
				storageFullBlock.UpdateIcon("Icons/Color/Check");
				storageFullBlock.UpdateText(T.On);
			}
			else
			{
				storageFullBlock.UpdateIcon("Icons/Color/Cross");
				storageFullBlock.UpdateText(T.Off);
			}
		}

		private void ToggleShowStorageFull()
		{
			if (isStorageFullWarningsOn)
			{
				isStorageFullWarningsOn = false;
				HideStorageFullNotification();
			}
			else
			{
				isStorageFullWarningsOn = true;
			}
			UpdateStorageFullBlock();
		}

		private void BuildColorBlock(List<UDB> blocks)
		{
			Craftable craftable = Demand.Craftable;
			if (craftable.ColorGroupId == null)
			{
				return;
			}
			if (colChoice == null)
			{
				colChoice = ColorChoice.ForGroup(S, craftable.ColorGroupId).WithAction(OnChangeColor).IncludeRandom(rand: true);
				if (Demand.ColorId != null)
				{
					ColorDef def = Palette.Get(Demand.ColorId);
					colChoice.WithSelectedColor(def);
				}
			}
			blocks.Add(colChoice.GetUIBlock());
		}

		private void OnChangeColor(ColorDef def)
		{
			Demand.ColorId = def?.Id;
			UpdateUIBlock(wasUpdated: true);
		}

		public void StopProducing()
		{
			Demand = null;
			Progress = 0f;
			colChoice = null;
			CancelHaulingAd("Stopped production");
			HideIcon();
			HideStorageFullNotification();
			UpdateExtraInfo();
			deficitEv.Clear();
			if (Arrays.IsEmpty(ingredients))
			{
				return;
			}
			for (int i = 0; i < ingredients.Length; i++)
			{
				Mat mat = ingredients[i];
				if (mat.StackSize != 0)
				{
					EntityUtils.SpawnRawMaterial(mat, Tile.Transform.WorkSpot, 0.5f, skipCheck: true);
				}
			}
			if (demandBlock != null)
			{
				demandBlock.NeedsListRebuild = true;
			}
			ingredients = null;
			ingredientBlocks.Clear();
		}

		private void CancelHaulingAd(string why)
		{
			deficitEv.Clear();
			if (currentAd != null)
			{
				if (currentAd.IsCompleted || currentAd.IsCancelled)
				{
					D.Err("Completed or cancelled ad still on device! {0}", currentAd);
				}
				S.Adverts.Cancel(currentAd, why);
			}
		}

		public new void GetUIDetails(List<UDB> res)
		{
			if (prodBlock == null)
			{
				prodBlock = UDB.Create(this, UDBT.DText, "Icons/Color/Count", T.TotalItemsProduced);
			}
			prodBlock.UpdateText(Units.XNum(TotalProduced));
			res.Add(prodBlock);

			// TODO Create separate SculptorSlotsComp and show this UI there?
			// This part goes in UpdateUIBlock
			// dataBlock.WithRange(0f, slots.Count);
			// if (dataBlock.UpdateValue(UsedSlotCount))
			// {
			// 	dataBlock.NeedsCompleteRebuild = true;
			// }
			// This part goes in GetUIDetails
			// for (int i = 0; i < slots.Count; i++)
			// {
			// 	Slot slot = slots[i];
			// 	res.Add(slot.DataBlock);
			// 	slot.AddSideReservationUI(this, res);
			// }

			if (Demand == null)
			{
				LoadCraftingOptions(res);
			}
			else
			{
				LoadDemandOptions(res);
			}
		}

		private bool RebuildIngredientsReq()
		{
			MissingMats.Clear();
			Mat[] array = ingredients;
			for (int i = 0; i < array.Length; i++)
			{
				Mat mat = array[i];
				if (mat.Diff >= 1)
				{
					MissingMats.Add(new MatRequest
					{
						Type = mat.Type,
						Amount = mat.Diff,
						IsAmountOptional = true,
						Requester = this
					});
				}
			}
			return MissingMats.Count == 0;
		}

		private bool CheckMissingIngredients(bool checkIfProducedEnough)
		{
			if (checkIfProducedEnough)
			{
				CraftingDemand demand = Demand;
				if (demand == null)
				{
					return false;
				}
			}
			Advert advert = currentAd;
			if (advert != null && advert.IsCancelled)
			{
				currentAd = null;
			}
			Advert advert2 = currentAd;
			if (advert2 != null && !advert2.IsCompleted)
			{
				return false;
			}
			bool flag = false;
			Mat[] array = ingredients;
			foreach (Mat mat in array)
			{
				if (mat.Diff > 0)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				if (showingStorageFullIcon)
				{
					HideInfoIcon();
					showingStorageFullIcon = false;
				}
				return true;
			}
			RebuildIngredientsReq();
			CancelHaulingAd("Has missing ingredients");
			currentAd = CreateAdvert("Hauling", "Icons/Color/Store", T.HaulIngredients).WithPromise(NeedIdH.Purpose, 5).MarkAsWork().WithPriority(craftPriority)
				.AndThen("GatherRawMaterials", T.GatherMaterials, NeedIdH.Purpose, 5)
				.Publish();
			return false;
		}

		private void ShowIdleNotification()
		{
			if (!isShowingIdle && isIdleWarningsOn)
			{
				isShowingIdle = true;
				deficitEv.Clear();
				S.Sig.IdleChanged.Send(this, p2: true);
			}
		}

		private void HideIdleNotification()
		{
			if (isShowingIdle)
			{
				isShowingIdle = false;
				S.Sig.IdleChanged.Send(this, p2: false);
			}
		}

		private void ShowStorageFullNotification()
		{
			if (!isStorageFullWarningsOn || storageFullNotification != null)
			{
				return;
			}
			// deficitEv.Clear();
			if (isIdleWarningsOn)
			{
				if (storageFullGroupId == null)
				{
					storageFullGroupId = "storage_full_" + base.Entity.DefinitionId + "_" + Demand.Product.Id;
				}
				if (storageFullTitle == null)
				{
					storageFullTitle = "sculpturebench.storage_full_notif".T(base.Entity.Definition.NameT, Demand.Product.NameT);
				}
				storageFullNotification = EventNotification.CreateGroupable(S.Ticks, storageFullGroupId, UDB.Create(this, UDBT.IEvent, base.Entity.Definition.Preview, storageFullTitle), Priority.Low);
				S.Sig.AddEvent.Send(storageFullNotification);
			}
		}

		private void HideStorageFullNotification()
		{
			if (storageFullNotification != null)
			{
				S.Sig.RemoveEvent.Send(storageFullNotification);
				storageFullNotification = null;
			}
		}

		private ElectricNodeComp eNode;

		public override void Receive(IComponent sender, int message) {
            if (message == MsgIdH.ElectricNodeAdded) {
                if (sender is ElectricNodeComp enode) {
                    eNode = enode;
                    enode.AddAfterTick(this, AfterTickGrid);
                    AfterTickGrid();
                }
            }
        }

		public void AfterTickGrid() {
			if (Demand == null)
			{
				if (currentAd != null)
				{
					D.ErrSoft("Device not configured but current ad is not null {0}!", currentAd);
					CancelHaulingAd("Device not configured");
				}
				if (!showingUnconfiguredIcon)
				{
					HideIcon();
					showingUnconfiguredIcon = true;
					ShowInfoIcon("Icons/Color/NotConfigured", T.InfoDeviceUnconfigured);
				}
				eNode.SetConsumerWantedInput(0f);
				return;
			} else {
				if (isShowingIdle)
				{
					HideIdleNotification();
				}
			}
			D.Ass(!Arrays.IsEmpty(ingredients), "Ingredients empty for {0}", Demand.Craftable.Id);
			if (!CheckMissingIngredients(checkIfProducedEnough: false))
			{
				eNode.SetConsumerWantedInput(0f);
				return;
			}
			if (Demand.Craftable == null)
			{
				D.Err("Broken demand in a crafter: {0}", Demand);
				StopProducing();
				eNode.SetConsumerWantedInput(0f);
				return;
			}
			// Check available storage
			if (producedSculpture != null) {
				int pos = FindAvailableStorageTile(producedSculpture);
				if (pos == Pos.Invalid) {
					ShowStorageFullNotification();
					if (!showingStorageFullIcon)
					{
						HideIcon();
						showingStorageFullIcon = true;
						ShowInfoIcon("Icons/Color/StorageFull", "sculpturebench.storage_full".T());
					}
					eNode.SetConsumerWantedInput(0f);
					return;
				}
			}
			HideStorageFullNotification();
			if (!isSculpting)
			{
				eNode.SetConsumerWantedInput(0f);
				return;
			}
			float energyCost = Demand.Craftable.EnergyCost;
			if (eNode.IsConsuming) {
				float num = (float) 10f / Consts.TicksPerHour / Demand.Craftable.ProductionTimeHours;
				num *= Tunable.Float(783785663) * craftingSpeedMultiplier;
				Progress += num;
				if (Progress >= 1f)
				{
					SpawnCraftable(Demand.Craftable);
					eNode.SetConsumerWantedInput(0f);
				}
				UpdateUIBlock(wasUpdated: true);
				return;
			} else {
				eNode.SetConsumerWantedInput(energyCost);
			}
		}

		private void SpawnCraftable(Craftable craftable) {
			Def def = craftable.ProductDef;
			D.Warn("Variations: " + def.HasVariations + "; " + def.Variations.Count);
			Being being = slots[0].ContainedBeing;
			if (being == null) {
				D.Err("No being in sculptor comp work slot when spawning craftable!");
			}
			Tile sculptureTile;
			if (producedSculpture != null) {
				sculptureTile = producedSculpture;
			} else {
				if (def.HasVariations)
				{
					if (Demand.ColorId != null)
					{
						foreach (Def variation in def.Variations)
						{
							if (variation.ColorId == Demand.ColorId)
							{
								def = variation;
								break;
							}
						}
					}
					else
					{
						D.Warn("Def: " + def.Id + "; variations: " + def.Variations.Count);
						CalculateEfficiency(being);
						Dictionary<SculptureQuality, float> chanceDict = SculptureQualities.All.Values.ToDictionary(
							keySelector: q => q,
							elementSelector: q => q.CraftingChanceCurve.Evaluate(averageEfficiency)
						);
						SculptureQuality quality = S.Rng.WeightedFrom(chanceDict);
						Def original = def;
						def = original.Variations.Single(d => d.Id.EndsWith(quality.Id));
						if (def == null) {
							D.Err("No sculpture variation matches quality {0} for type {1}", quality.Id, original.Id);
						}
						D.Warn("Variation: " + def.Id + "; material: " + def.MatType);
					}
				}
				sculptureTile = EntityUtils.CreatePrototype<Tile>(def);
			}
			if (sculptureTile.HasComponent<CreatorInfoComp>()) {
				sculptureTile.GetComponent<CreatorInfoComp>().SetCreators(sculptorIds.ToArray(), sculptorNames.ToArray());
			}
			// Is there room to place the sculpture?
			int pos = FindAvailableStorageTile(sculptureTile);
			if (pos == Pos.Invalid) {
				D.Err("Cannot place tile {0}", sculptureTile);
				// Save sculpture and show warning
				producedSculpture = sculptureTile;
				Progress = 0.99999f;
				return;
			}
			CmdPlaceTile cmd = new CmdPlaceTile(pos, sculptureTile, warn: false, instant: true);
			cmd.Execute(S);
			
			sculptorIds = null;
			sculptorNames = null;
			Progress = 0f;
			averageEfficiency = 0;
			startedSculpting = 0;
			for (int i = 0; i < ingredients.Length; i++)
			{
				Mat mat = ingredients[i];
				mat.StackSize = 0;
				ingredients[i] = mat;
			}
			S.Sig.EntityProduced.Send(Demand.Product, 1);
			Demand.AmountProducedThis++;
			TotalProduced++;
			Tile.Damageable.AddWear();
		}

		private int FindAvailableStorageTile(Tile forTile) {
			int pos = Pos.Invalid;
			foreach (Vector2 offset in sculptureStorageOffsets) {
				pos = RotatedSculptureSpot(offset);
				bool canPlace = CmdPlaceTile.CanPlace(pos, forTile, forTile.Transform.LayerId, warn: false);
				// D.Warn("Can place tile at offset {0}: {1}", offset, canPlace);
				if (canPlace) {
					return pos;
				} else {
					pos = Pos.Invalid;
				}
			}
			return pos;
		}

		private int RotatedSculptureSpot(Vector2 offset)
		{
			int offsetX = Mathf.RoundToInt(offset.x);
			int offsetY = Mathf.RoundToInt(offset.y);
			TileTransformComp ttc = base.Entity.GetComponent<TileTransformComp>();
			switch (ttc.Rotation)
			{
			case Facing.Type.Down:
				return Pos.NeighborAt(base.Entity.PosIdx, offsetX, offsetY);
			case Facing.Type.Up:
				return Pos.NeighborAt(base.Entity.PosIdx, ttc.Width - 1 - offsetX, ttc.Height - offsetY - 1);
			case Facing.Type.Left:
				return Pos.NeighborAt(base.Entity.PosIdx, offsetY, ttc.Width - offsetX - 1);
			case Facing.Type.Right:
				return Pos.NeighborAt(base.Entity.PosIdx, ttc.Height - offsetY - 1, offsetX);
			default:
				D.Err("Unhandled facing in workspot: {0}", ttc.Rotation);
				return base.Entity.PosIdx;
			}
		}

		public void OnAdvertLoad(Advert ad)
		{
			currentAd = ad;
		}

		public void OnAdvertCancelled(Advert advert)
		{
			if (advert != currentAd)
			{
				D.Err("Unexpected ad in OnCancel: {0}. Current: {1}", advert, currentAd);
			}
			else
			{
				currentAd = null;
			}
		}

		public void OnAdvertComplete(Advert ad)
		{
			if (ad != currentAd)
			{
				D.Err("Unexpected ad in OnComplete: {0}. Current: {1}", ad, currentAd);
			}
			else
			{
				currentAd = null;
			}
		}

		public bool CanCopyTo(ICopyableComp target)
		{
			if (target is SculptorComp sculptorComp)
			{
				return sculptorComp.Entity.Definition == base.Entity.Definition;
			}
			return false;
		}

		public bool CopyConfigTo(ICopyableComp target)
		{
			bool result = false;
			if (target is SculptorComp sculptorComp && sculptorComp.Entity.Definition == base.Entity.Definition)
			{
				if (sculptorComp.craftPriority != craftPriority)
				{
					sculptorComp.OnAdvertPriorityChange(craftPriority);
					result = true;
				}
				if (sculptorComp.isStorageFullWarningsOn != isStorageFullWarningsOn)
				{
					sculptorComp.isStorageFullWarningsOn = isStorageFullWarningsOn;
					result = true;
				}
				if (sculptorComp.Demand == Demand)
				{
					return result;
				}
				bool num = sculptorComp.Demand?.craftableId != Demand?.craftableId;
				if (num)
				{
					sculptorComp.StopProducing();
				}
				sculptorComp.Demand = CraftingDemand.Copy(Demand);
				if (num)
				{
					sculptorComp.SetIngredients();
				}
				sculptorComp.UpdateProgressBar();
				sculptorComp.UpdateExtraInfo();
				result = true;
			}
			return result;
		}

		public void CancelAdvert(string why)
		{
			if (HasCancellableAd)
			{
				S.Adverts.Cancel(currentAd, why);
			}
		}

		public void ContextActions(List<UDB> res)
		{
			if (Tile.IsConstructed && Tile.ENode.IsReachable)
			{
				GetUIDetails(res);
			}
		}

		public void RelocateTo(Entity target)
		{
			SculptorComp sculptorComp = target.GetComponent<SculptorComp>();
			if (sculptorComp != null) {
				sculptorComp.Demand = Demand;
				sculptorComp.Progress = Progress;
				sculptorComp.TotalProduced = TotalProduced;
				sculptorComp.averageEfficiency = averageEfficiency;
				sculptorComp.startedSculpting = startedSculpting;
				sculptorComp.sculptorIds = sculptorIds;
				sculptorComp.sculptorNames = sculptorNames;
				sculptorComp.ingredients = ingredients;
				sculptorComp.producedSculpture = producedSculpture;
			}
			CopyConfigTo(target.GetComponent<ICopyableComp>());
		}

		public void AfterInitDef(Def def, ComponentConfig config)
		{
			string[] stringSet = config.GetStringSet("CompatibleTypes", null);
			if (stringSet == null)
			{
				D.Err("SculptorComp does not have compatible types! {0}", def.Id);
				return;
			}
			string[] array = stringSet;
			foreach (string key in array)
			{
				if (!Craftable.CraftersByType.TryGetValue(key, out var value))
				{
					value = new List<Def>();
					Craftable.CraftersByType.Add(key, value);
				}
				value.Add(def);
			}
		}

		public bool ProvideRequestedMat(UnstoredMatComp unstoredMat, Being worker)
		{
			D.Ass(ingredients != null, "Crafter taking ingredients from being while having null ingredients");
			for (int i = 0; i < ingredients.Length; i++)
			{
				Mat mat = ingredients[i];
				if (mat.Type != unstoredMat.Type)
				{
					continue;
				}
				int diff = mat.Diff;
				if (diff > 0)
				{
					int wantedAmount = Mathf.Min(diff, unstoredMat.StackSize);
					wantedAmount = unstoredMat.Take(mat.Type, null, wantedAmount, currentAd);
					mat.StackSize += wantedAmount;
					ingredients[i] = mat;
					if (mat.StackSize == mat.MaxStackSize)
					{
						deficitEv.ClearIf(mat.Type);
					}
					UpdateUIBlock(wasUpdated: true);
				}
			}
			bool num = RebuildIngredientsReq();
			if (num)
			{
				HideIcon();
			}
			return num;
		}

		private void HideIcon()
		{
			HideInfoIcon();
			showingStorageFullIcon = false;
			showingUnconfiguredIcon = false;
		}

		public void ClearLackNotification(MatType type)
		{
			deficitEv.ClearIf(type);
		}

		public void NotifyMatDeficit(MatType type)
		{
			deficitEv.Notify(type);
		}

		public void OnAdvertPriorityChange(int priority)
		{
			craftPriority = priority;
			priorityBlock?.UpdateValue(priority);
		}

		public void BeginSculpting(Being worker){
			startedSculpting = Progress;
			AddSculptor(worker);
			worker.Graphics.SetFacing(SpotRotation);
			worker.Transform.TeleportTo(base.Tile.Position + offsets[(int)base.Tile.Transform.Rotation]);
			// base.Tile.Light.SwitchLight(on: true);
			isSculpting = true;
		}

		public bool Sculpt(Being worker){
			// Do sculpting work
			worker.Graphics.Punch(Facing.ToOffset(SpotRotation, 1f), 0.15f, Rng.URange(0.8f, 1.2f), Rng.URange(0.8f, 1.2f));
			if (Demand == null) {
				return false;
			}
			if (!Tile.ENode.IsReachable) {
				return false;
			}
			if (IsMissingMaterials()) {
				return false;
			}
			return !showingStorageFullIcon;
		}

		public void EndSculpting(Being worker){
			if (Progress > 0 && Progress < 1f) {
				// Calculate new average efficiency
				CalculateEfficiency(worker);
			}
			// base.Tile.Light.SwitchLight(on: false);
			isSculpting = false;
		}

		private void CalculateEfficiency(Being being) {
			// Get chance from 0-1
			float efficiency = being.Skills.EfficiencyOf(SculpturesMod.SkillIdArtistic) * 0.5f;
			if (being.Traits.HasTrait(TraitCreative.Id)) {
				efficiency = Mathf.Min(efficiency + TraitCreative.SkillChanceAdd, 1f);
			}
			float highSkill = 0;
			float skillLevel = 0;
			foreach (Skill skill in being.Skills.Skills.Values) {
				if (skill.Type.IdHash == SculpturesMod.SkillIdArtistic) {
					continue;
				}
				skillLevel = skill.Level / ((float) skill.MaxLevel) * 10f;
				if (skill.Level > highSkill) {
					highSkill = skill.Level;
				}
			}
			efficiency = Mathf.Min(efficiency + highSkill * 0.01f, 1f);
			float beingProgress = Progress - startedSculpting;
			float beingContribution = efficiency * beingProgress;
			averageEfficiency += beingContribution;
		}

		private void AddSculptor(Being being) {
			if (sculptorIds == null) {
				sculptorIds = new List<int>();
			}
			int id = being.Id;
			if (sculptorIds.Contains(id)) {
				return;
			}
			sculptorIds.Add(id);
			if (sculptorNames == null) {
				sculptorNames = new List<string>();
			}
			string name = being.Persona.Name;
			if (!sculptorNames.Contains(name)) {
				sculptorNames.Add(name);
			}
		}

		private bool IsMissingMaterials() {
			Advert advert = currentAd;
			if (advert != null && advert.IsCancelled)
			{
				currentAd = null;
			}
			Advert advert2 = currentAd;
			if (advert2 != null && !advert2.IsCompleted)
			{
				return true;
			}
			Mat[] array = ingredients;
			foreach (Mat mat in array)
			{
				if (mat.Diff > 0)
				{
					return true;
				}
			}
			return false;
		}
	}
}
