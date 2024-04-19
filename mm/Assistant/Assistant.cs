using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace Assistant
{
    public class DriverAssistOptions
    {
        [Header("Driving")]
        [Draw("Enabled")] public bool driving = false;
        [Draw("Max on the 1st lap", VisibleOn = "#MaxDrivingFirtLapVisible|True")] public bool boostDrive = false;

        [Draw("Smart tyre", VisibleOn = "driving|True")] public bool autoTyre = false;
        [Draw("Hold tyre temperature", DrawType.Slider, Min = 0, Max = 100, Precision = 0, VisibleOn = "#TyreTemperatureVisible|True")] public float temperature = 70f;

        [Header("Engine")]
        [Draw("Enabled")] public bool engine = false;
        [Draw("Max on the 1st lap", VisibleOn = "#MaxEngineFirtLapVisible|True")] public bool boostEngine = false;
        [Draw("Smart engine", VisibleOn = "engine|True")] public bool smartEngine = false;


        [Header("Pitstop")]
        [Draw("Planned pitstop", VisibleOn = "engine|True")] public bool plannedPitstop = false;
        [Draw("Auto set pitstops", VisibleOn = "#OnlapVisible|True")] public bool autoPitstop;
        [Draw("Stint length multiplier", DrawType.Slider, Min = 0.5, Max = 1.5, Precision = 2, VisibleOn = "#StintLengthVisible|True")] public float stintLength = 1f;
        [Draw("Set", VisibleOn = "#StintLengthVisible|True")] public bool setPitstop;

        [Draw("Hold fuel lap delta", DrawType.Slider, Min = -1, Max = 1, Precision = 2, VisibleOn = "#HoldfuelVisible|True")] public float fuel = 0f;
        [Draw("On lap", DrawType.Field, Min = 1, Max = 1000, Precision = 0, VisibleOn = "plannedPitstop|True")] public int pitstopOnLap = 100;


        [Draw("Next pitstops", DrawType.Field, Min = 1.0, Precision = 0, VisibleOn = "#OnlapVisible|True")]
        public int[] nextPitstops = { };

        [Header("ERS options")]
        [Draw("Assist ERS")] public bool ers = false;
        [Draw("Do not spend excess power on hybride", VisibleOn = "ers|True")] public bool doNotSpendExcessOnHybride = false;

        bool OnlapVisible => engine && plannedPitstop;
        bool HoldfuelVisible => engine && !plannedPitstop;
        bool TyreTemperatureVisible => driving && !autoTyre;
        bool StintLengthVisible => autoPitstop && OnlapVisible;
        bool MaxEngineFirtLapVisible => engine && !smartEngine;
        bool MaxDrivingFirtLapVisible => driving && !autoTyre;


        private void SetPitStop()
        {
            if (setPitstop)
            {
                int lapLeft;
                setPitstop = false;
                if (Game.instance.sessionManager.eventDetails.currentSession.sessionType == SessionDetails.SessionType.Race)
                {
                    SessionManager session = Game.instance.sessionManager;


                    if (Main.IsEnduranceSeries(session.championship.series))
                    {
                        lapLeft = Mathf.RoundToInt((1f - session.GetNormalizedSessionTime() * Game.instance.sessionManager.duration / session.estimatedLapTime));
                    }
                    else
                    {
                        lapLeft = session.lapCount - session.lap;

                    }

                    //I hope this is right ??
                    float maxFuel = Mathf.RoundToInt(session.championship.rules.fuelLimitForRaceDistanceNormalized * session.lapCount);

                    if (maxFuel != 0 && lapLeft != 0 && stintLength != 0)
                    {
                        int pitPerRace = Mathf.RoundToInt(lapLeft / (maxFuel * stintLength));



                        List<int> l = new List<int>();

                        for (int i = 1; i <= pitPerRace; i++)
                        {
                            int temp = session.lap + Mathf.RoundToInt(lapLeft * i / (pitPerRace + 1));

                            if (i == 1)
                            {
                                pitstopOnLap = temp;
                            }
                            else
                            {
                                l.Add(temp);
                            }
                        }

                        nextPitstops = l.ToArray();

                    }
                }
            }
        }


        public void OnChange()
        {
            SetPitStop();
        }

    }

    public class ManagementAssistOptions
    {
        [Header("Performance")]
        [Draw("Improve")] public bool improvePerformance = false;
        [Space(10)]
        [Header("Reliability")]
        [Draw("Improve")] public bool improveReliability = false;
        [Draw("Improve to", DrawType.Slider, Min = 0, Max = 100, Precision = 0, VisibleOn = "improveReliability|True")] public float improveReliabilityTo = 80f;
    }

    public class PracticeAssistOptions
    {
        [Header("Practice")]
        [Draw("Enabled")] public bool enabled = false;
        [Header("Knowledge")]
        [Draw("Priority")] public KnowledgePriority knowledgePriority = new KnowledgePriority();
    }

    public class KnowledgePriority
    {
        [Draw("Qualification")] public bool qualification;
        [Draw("Race")] public bool race;
        [Draw("First tyre")] public bool firstTyre;
        [Draw("Second tyre")] public bool secondTyre;
        [Draw("Third tyre")] public bool thirdTyre;
    }

    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("AI race control")] public bool aiControl = false;
        [Draw("Manual pitstop", VisibleOn = "aiControl|True")] public bool manualPitstop = false;
        [Draw("Driver 1 Assistance Options", Box = true, Collapsible = true, InvisibleOn = "aiControl|True")] public DriverAssistOptions driver1AssistOptions = new DriverAssistOptions();
        [Draw("Driver 2 Assistance Options", Box = true, Collapsible = true, InvisibleOn = "aiControl|True")] public DriverAssistOptions driver2AssistOptions = new DriverAssistOptions();
        [Draw("Practice Assistance Options", Box = true, Collapsible = true)] public PracticeAssistOptions practiceAssistOptions = new PracticeAssistOptions();
        [Draw("Management Assistance Options", Box = true, Collapsible = true)] public ManagementAssistOptions managementAssistOptions = new ManagementAssistOptions();

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public void OnChange()
        {
            driver1AssistOptions.OnChange();
            driver2AssistOptions.OnChange();
        }
    }

#if DEBUG
    [EnableReloading]
#endif
    static class Main
    {
        public static bool enabled;
        public static Settings settings;
        public static UnityModManager.ModEntry.ModLogger logger;
        public static bool hasEnduranceSeries2;
        public static Championship.Series EnduranceSeries2;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            settings = Settings.Load<Settings>(modEntry);

            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            logger = modEntry.Logger;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
#if DEBUG
            modEntry.OnUnload = Unload;
#endif
            var names = Enum.GetNames(typeof(Championship.Series));
            if (names.Contains("EnduranceSeries2"))
            {
                hasEnduranceSeries2 = true;
                EnduranceSeries2 = (Championship.Series)Enum.Parse(typeof(Championship.Series), "EnduranceSeries2");
            }

            return true;
        }
#if DEBUG
        static bool Unload(UnityModManager.ModEntry modEntry)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.UnpatchAll(modEntry.Info.Id);
            return true;
        }
#endif
        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Draw(modEntry);

        }

        public static bool IsEnduranceSeries(Championship.Series series)
        {
            return series == Championship.Series.EnduranceSeries || hasEnduranceSeries2 && series == EnduranceSeries2;
        }
    }

    [HarmonyPatch(typeof(PartImprovement), "Update")]
    static class PartImprovement_Update_Patch
    {
        static void Prefix(PartImprovement __instance, Team ___mTeam)
        {
            if (Game.instance.time.isPaused || !___mTeam.IsPlayersTeam()) return;

            if (Main.settings.managementAssistOptions.improvePerformance)
            {
                var partList = new List<CarPart>();

                for (int i = 0; i < CarManager.carCount; i++)
                {
                    var car = ___mTeam.carManager.GetCar(i);

                    for (int index = 0; index < car.seriesCurrentParts.Length; ++index)
                    {
                        var p = car.seriesCurrentParts[index];
                        if (p != null && !p.isBanned && p.stats.performance < p.stats.maxPerformance)
                        {
                            partList.Add(p);
                        }
                    }
                }

                if (partList.Count == 0)
                {
                    goto Next;
                }

                __instance.RemoveAllPartImprove(CarPartStats.CarPartStat.Performance);

                var parts = partList.OrderBy(x => x.stats.performance).ToArray();

                foreach (var part in parts)
                {
                    if (__instance.HasAvailableSlot(CarPartStats.CarPartStat.Performance))
                    {
                        __instance.AddPartToImprove(CarPartStats.CarPartStat.Performance, part);
                        continue;
                    }
                    break;
                }
            }

        Next:

            if (Main.settings.managementAssistOptions.improveReliability)
            {
                var partList = new List<CarPart>();

                for (int i = 0; i < CarManager.carCount; i++)
                {
                    var car = ___mTeam.carManager.GetCar(i);

                    for (int index = 0; index < car.seriesCurrentParts.Length; ++index)
                    {
                        var p = car.seriesCurrentParts[index];
                        if (p != null && !p.isBanned && p.stats.reliability < p.stats.maxReliability)
                        {
                            partList.Add(p);
                        }
                    }
                }

                if (partList.Count == 0)
                {
                    return;
                }

                __instance.RemoveAllPartImprove(CarPartStats.CarPartStat.Reliability);

                var parts = partList.OrderBy(x => x.stats.reliability).ToArray();

                var improveReliabilityTo = Main.settings.managementAssistOptions.improveReliabilityTo / 100f;

                __instance.SplitMechanics(0f);
                int limit = Mathf.FloorToInt(__instance.GetWorkRate(CarPartStats.CarPartStat.Reliability) / (improveReliabilityTo - parts[0].stats.reliability));
                int j = 1;
                foreach (var part in parts)
                {
                    __instance.AddPartToImprove(CarPartStats.CarPartStat.Reliability, part);
                    j++;
                    if (j > limit || !__instance.HasAvailableSlot(CarPartStats.CarPartStat.Reliability))
                    {
                        break;
                    }
                }

                if (__instance.partsToImprove[1].Count > 0 && __instance.partsToImprove[3].Count > 0)
                {
                    if (__instance.partsToImprove[1].Count(x => x.stats.reliability < improveReliabilityTo) > 0)
                    {
                        __instance.SplitMechanics(0f);
                        var workRate = __instance.GetWorkRate(CarPartStats.CarPartStat.Reliability) + __instance.GetChiefMechanicWorkRate(CarPartStats.CarPartStat.Reliability);
                        var val = __instance.partsToImprove[1].Select(x => (improveReliabilityTo - x.stats.reliability) / workRate).OrderByDescending(x => x).First();
                        __instance.SplitMechanics(1f - Mathf.Clamp01(val));
                    }
                    else
                    {
                        __instance.SplitMechanics(1f);
                    }
                }
            }
        }
    }

    static class Assistant
    {

        internal enum Behaviour : int
        {
            Defend = 0,
            Attack = 1,
            Both = 2,
            Drive = 3,
            Save = 4
        }



        internal static Behaviour SelectBehaviour(RacingVehicle vehicle)
        {

            if (Game.instance.sessionManager.flag == SessionManager.Flag.SafetyCar || Game.instance.sessionManager.flag == SessionManager.Flag.VirtualSafetyCar || (vehicle.timer.currentSector == Game.instance.sessionManager.yellowFlagSector && Game.instance.sessionManager.flag == SessionManager.Flag.Yellow))
            {
                return Behaviour.Save;
            }

            float gapAhead = GetGapAhead(vehicle);
            float gapBehind = GetGapBeheind(vehicle);

            bool attack = gapAhead < 0.6f || gapBehind < 0.5f;
            bool defend = gapBehind < 0.8f;
            bool save = false;

            float pace = vehicle.performance.estimatedBestLapTime;
            float paceAhead = pace;

            int teamID = vehicle.driver.contract.GetTeam().teamID;
            bool teamMateAhead = vehicle.standingsPosition != 1 && vehicle.ahead.driver.contract.GetTeam().teamID == teamID;
            RacingVehicle vehicleBeheind = vehicle.behind;
            RacingVehicle vehicleAhead = vehicle.ahead;
            bool teamMateBeheind = vehicle.standingsPosition != Game.instance.sessionManager.GetVehicleCount() && vehicleBeheind != null && vehicleBeheind.movementEnabled && vehicleBeheind.driver.contract.GetTeam().teamID == teamID;

            if(defend && teamMateBeheind)
            {
                defend = false;
                save = true;
            }

            if (!defend && teamMateAhead && ! (vehicleAhead.strategy.teamOrders == SessionStrategy.TeamOrders.AllowTeamMateThrough))
            {
                attack = false;
                save = true;
            }

            if (vehicle.standingsPosition != 1)
            {
                paceAhead = vehicle.ahead.performance.estimatedBestLapTime;
            }

            //Do not waste fuel and tyre if vehicle ahead is too fast if we're on the first half of the race
            if (attack && gapAhead > 0.2f && pace <= paceAhead + 0.5f && GetLapsRemainingDecimal(vehicle) < vehicle.timer.lap)
            {
                attack = false;
                save = true;
            }
            

            float paceBeheind = pace;

            if (vehicle.standingsPosition != Game.instance.sessionManager.GetVehicleCount())
            {
                paceBeheind = vehicle.behind.performance.estimatedBestLapTime;
            }

            if (defend && paceBeheind + 0.2f < pace)
            {
                defend = false;
            }
            

            //If the vehicle ahead is our teammate and we have to let him ahead, we save fuel and tyre
            if (!defend && teamMateAhead && gapAhead < 2f)
            {
                return Behaviour.Save;
            }

            if (attack && defend) return Behaviour.Both;

            if (defend) return Behaviour.Defend;

            if (save) return Behaviour.Save;

            if (attack) return Behaviour.Attack;



            return Behaviour.Drive;

        }

        internal static float GetGapAhead(RacingVehicle vehicle)
        {
            int teamID = vehicle.driver.contract.GetTeam().teamID;
            float gapAhead = vehicle.timer.gapToAhead;
            if (vehicle.standingsPosition == 1)
            {
                gapAhead = 999f;
            }

            if (vehicle.standingsPosition != 1 && vehicle.ahead.driver.contract.GetTeam().teamID == teamID)
            {
                gapAhead = 999f;
            }

            return gapAhead;
        }

        internal static float GetGapBeheind(RacingVehicle vehicle)
        {

            int teamID = vehicle.driver.contract.GetTeam().teamID;
            RacingVehicle vehicleBeheind = vehicle.behind;
            float gapBehind = 999f;

            //I can't find how to check if a vehicle has crashed, but movementEnabled seems to be false if a vehicle has crashed
            // So I will use that i guess
            if (!(vehicle.behind == null) && vehicleBeheind.movementEnabled)
            {
                gapBehind = vehicle.timer.gapToBehind; ;
            }

            if (vehicle.standingsPosition != Game.instance.sessionManager.GetVehicleCount() && vehicle.behind.driver.contract.GetTeam().teamID == teamID)
            {
                gapBehind = 999f;
            }

            return gapBehind;

        }

        internal static float getMinGap(RacingVehicle vehicle)
        {

            float minGap = Math.Min(GetGapAhead(vehicle), GetGapBeheind(vehicle));

            return minGap;
        }


        internal class TyreLog
        {
            public TyreSet tyre;
            public float temp;
            public DateTime time;
        }
        


        internal static readonly TyreLog tyre1 = new TyreLog();
        internal static readonly TyreLog tyre2 = new TyreLog();

        internal static void AssistDrive(DriverAssistOptions options, RacingVehicle vehicle, TyreLog tyreLog)
        {
            if (!options.driving) return;

            if ((Game.instance.time.now - tyreLog.time).TotalSeconds < 10)
                return;

            if (Game.instance.sessionManager.flag == SessionManager.Flag.SafetyCar || Game.instance.sessionManager.flag == SessionManager.Flag.VirtualSafetyCar)
            {
                vehicle.performance.drivingStyle.SetDrivingStyle(DrivingStyle.Mode.BackUp);
                return;
            }

            var mode = vehicle.performance.drivingStyleMode;
            var tyre = vehicle.setup.tyreSet;
            float temp = tyre.GetTemperature();
            if (tyreLog.tyre != tyre)
            {
                tyreLog.tyre = tyre;
                tyreLog.temp = temp;
                tyreLog.time = Game.instance.time.now;
                mode = DrivingStyle.Mode.Neutral;
            }

            var m = mode;
            var changeRate = 0.08f;
            var changeRate2 = changeRate * 2;
            var tempChangeRate = (float)((temp - tyreLog.temp) / (Game.instance.time.now - tyreLog.time).TotalMinutes);

            var t = options.temperature / 100f;

            if (options.autoTyre)
            {

                if (Game.instance.sessionManager.eventDetails.currentSession.sessionType == SessionDetails.SessionType.Practice)
                {
                    t = 0.2f;
                }
                else
                {
                    Behaviour behaviour = SelectBehaviour(vehicle);

                    if (behaviour == Behaviour.Attack || behaviour == Behaviour.Both)
                    {
                        t = 0.7f;
                        if (temp < t)
                        {
                            mode = DrivingStyle.Mode.Attack;
                        }
                    }
                    else if (behaviour == Behaviour.Drive)
                    {
                        t = 0.5f;
                    }
                    else
                    {
                        t = 0.3f;
                    }

                    if (vehicle.timer.currentSector == Game.instance.sessionManager.yellowFlagSector && Game.instance.sessionManager.flag == SessionManager.Flag.Yellow)
                    {
                        t = 0.05f;
                    }

                    if (behaviour == Behaviour.Defend && temp > t)
                    {
                        mode = DrivingStyle.Mode.BackUp;
                    }
                }

            }

            if (temp < t + 0.04f && temp > t - 0.04f && tempChangeRate > -changeRate && tempChangeRate < changeRate)
            {
            }
            else if (temp < t - 0.4f)
            {
                mode = GetIncreaseDrivingStyle(GetIncreaseDrivingStyle(GetIncreaseDrivingStyle(mode)));
            }
            else if (temp < t - 0.2f)
            {
                if (tempChangeRate < changeRate2)
                    mode = GetIncreaseDrivingStyle(GetIncreaseDrivingStyle(mode));
            }
            else if (temp > t + 0.2f)
            {
                if (tempChangeRate > -changeRate2)
                    mode = GetDecreaseDrivingStyle(GetDecreaseDrivingStyle(mode));
            }
            else if (temp > t + 0.4f)
            {
                if (tempChangeRate > -changeRate2)
                    mode = GetDecreaseDrivingStyle(GetDecreaseDrivingStyle(GetDecreaseDrivingStyle(mode)));
            }
            else if (temp < t)
            {
                if (tempChangeRate < changeRate)
                    mode = GetIncreaseDrivingStyle(mode);
            }
            else if (temp > t)
            {
                if (tempChangeRate > -changeRate)
                    mode = GetDecreaseDrivingStyle(mode);
            }

            if (vehicle.timer.lap == 0 && options.boostDrive)
            {
                mode = DrivingStyle.Mode.Attack;
            }


            if (options.autoTyre && ! (Game.instance.sessionManager.eventDetails.currentSession.sessionType == SessionDetails.SessionType.Practice) )
            {

                if (temp > 0.80 && mode < DrivingStyle.Mode.Conserve)
                {
                    mode = DrivingStyle.Mode.Conserve;
                }
                else if (temp < 0.2 && mode > DrivingStyle.Mode.Push)
                {
                    mode = DrivingStyle.Mode.Push;
                }
                else
                {
                    Behaviour behaviour = SelectBehaviour(vehicle);

                    if(behaviour == Behaviour.Drive && temp < t + 0.1f && temp > t - 0.1f)
                    {
                        mode = DrivingStyle.Mode.Neutral;
                    }
                    //Do not ruine the tyre if we're not fighting for a position
                    if ((!(behaviour == Behaviour.Attack || behaviour == Behaviour.Both)) && mode == DrivingStyle.Mode.Attack)
                    {
                        mode = GetDecreaseDrivingStyle(mode);
                    }
                    else if ((!(behaviour == Behaviour.Defend || behaviour == Behaviour.Both)) && mode == DrivingStyle.Mode.BackUp)
                    {
                        mode = GetIncreaseDrivingStyle(mode);
                    }
                    else
                    {
                        //Do not eat the tyre if would make us pit too early
                        if (mode == DrivingStyle.Mode.Attack)
                        {
                            float tyreWear = tyre.GetCondition();

                            SessionTimer.PitstopData lastPit = vehicle.timer.currentPitstop;
                            int lastPitLap;
                            if (lastPit == null)
                            {
                                lastPitLap = 0;
                            }
                            else
                            {
                                lastPitLap = lastPit.lapNumber;
                            }

                            float num = vehicle.pathController.distanceAlongTrackPath01;
                            if (num == 1f)
                            {
                                num = 0f;
                            }



                            float nextPitLap = options.pitstopOnLap;

                            if (!options.plannedPitstop)
                            {
                                nextPitLap = Game.instance.sessionManager.lapCount;
                            }

                            float relayLength = nextPitLap - lastPitLap;
                            float lapsInRelay = vehicle.timer.lap + vehicle.pathController.distanceAlongTrackPath01 - lastPitLap;
                            float clifCondition = vehicle.setup.tyreSet.GetCliffCondition();



                            float relayPercent = 1;
                            if (relayLength > 0)
                            {
                                relayPercent = lapsInRelay / relayLength;

                                if (relayPercent > clifCondition && (relayPercent - clifCondition) > tyreWear - 0.05f)
                                {
                                    mode = DrivingStyle.Mode.Push;
                                }
                            }
                        }
                        else if (mode == DrivingStyle.Mode.Conserve && behaviour == Behaviour.Defend)
                        {
                            mode = DrivingStyle.Mode.BackUp;
                        }

                    }

                }
            }

            vehicle.performance.drivingStyle.SetDrivingStyle(mode);

            //            Main.logger.Log($"{m}->{mode} {tempChangeRate} {temp} {tyreLog.temp} {(Game.instance.time.now - tyreLog.time).TotalMinutes}");

            tyreLog.temp = temp;
            tyreLog.time = Game.instance.time.now;
        }

        public static DrivingStyle.Mode GetIncreaseDrivingStyle(DrivingStyle.Mode mode)
        {
            if (mode != DrivingStyle.Mode.Attack)
            {
                mode--;
            }

            return mode;
        }

        public static DrivingStyle.Mode GetDecreaseDrivingStyle(DrivingStyle.Mode mode)
        {
            if (mode != DrivingStyle.Mode.BackUp)
            {
                mode++;
            }

            return mode;
        }


        internal class RadioMessagePlannedPit : RadioMessage
        {

            public RadioMessagePlannedPit(RacingVehicle inVehicle, TeamRadio inTeamRadio) : base(inVehicle, inTeamRadio)
            {
                text = new TextDynamicData();
                //This is awfull
                text.textID = "We planned to pit this lap !";


            }

            public override bool AllowDuplicateDilemma()
            {
                return true;
            }

            public override void OnLoad()
            {
            }
            public override bool DontDelayForDriverFeedback()
            {
                return true;
            }

            private bool IsVehicleReadyToDisplayMessage()
            {
                return mVehicle.pathController.currentPathType == PathController.PathType.Track && !mVehicle.strategy.IsGoingToPit() && !mVehicle.timer.hasSeenChequeredFlag && base.isRaceSession && !mVehicle.behaviourManager.isOutOfRace;
            }

            protected override void OnSimulationUpdate() { }

            protected void FakeSendDilemma()
            {
                Game.instance.sessionManager.teamRadioManager.SendDilemma(this);
            }

            public void CreateDialogQuery()
            {
                DialogQuery dialogQuery = new DialogQuery();
                dialogQuery.AddCriteria("Source", (Game.instance.sessionManager.flag != SessionManager.Flag.SafetyCar) ? "VSCDilemma" : "SafetyCarDilemma");
                AddPersonCriteria(dialogQuery, mVehicle.driver);
                dialogRule = mQueryCreator.ProcessQueryWithOwnCriteria(dialogQuery, inDontIgnoreRules: false);
                if (dialogRule != null)
                {
                    personWhoSpeaks = mVehicle.driver;
                    FakeSendDilemma();
                }
            }
        }


        internal static void NotifyPit(DriverAssistOptions options, RacingVehicle vehicle)
        {

            if (options.plannedPitstop && options.pitstopOnLap == vehicle.timer.lap + 1)
            {
                TeamRadioManager manager = Game.instance.sessionManager.teamRadioManager;
                RadioMessagePlannedPit message = new RadioMessagePlannedPit(vehicle, vehicle.teamRadio);
                message.CreateDialogQuery();
            }

        }

        internal static int GetGatesRemaininginPowerMode(RacingVehicle vehicle)
        {
            DesignData designData = DesignDataManager.instance.GetDesignData();
            bool mIsAdvancedERSActive = vehicle.championship.rules.isERSAdvancedModeActive;
            Supplier supplier = ((!mIsAdvancedERSActive) ? null : vehicle.car.ChassisStats.supplierERSAdvanced);
            float powerDrainRate = designData.ERSDesignData.largeBatterySize / ((!mIsAdvancedERSActive) ? designData.ERSDesignData.powerModeRate : ((float)supplier.powerGates));

            float num = vehicle.ERSController.normalizedCharge * vehicle.ERSController.maxCharge;
            int num2 = 0;
            while (num >= 0f)
            {
                num -= powerDrainRate;
                num2++;
            }
            return num2 - 1;
        }

        internal static void AssistERS(DriverAssistOptions options, RacingVehicle vehicle)
        {
            
            if (!options.ers || Game.instance.sessionManager.eventDetails.currentSession.sessionType != SessionDetails.SessionType.Race)
            {
                vehicle.ERSController.autoControlERS = true;
                return;
            }


            if (vehicle.ERSController.state == ERSController.ERSState.Broken || vehicle.ERSController.state == ERSController.ERSState.NotVisible)
            {
                return;
            }

            //In game, the ERS should be set to "Auto" and not "manual". I guess the UI take the control if it's set to manual.
            vehicle.ERSController.autoControlERS = false;

            if (vehicle.ERSController.normalizedCharge < 0.10)
            {
                return;
            }



            bool hybrideEnabled = vehicle.championship.rules.isHybridModeActive;

            bool powerEnabled = vehicle.championship.rules.isEnergySystemActive && vehicle.timer.lap > 0;



            float lapLeft = getLapLeft(options, vehicle);


            if (hybrideEnabled)
            {

                if(vehicle.timer.lap == 0 && vehicle.championship.rules.raceStart == ChampionshipRules.RaceStart.StandingStart) //Can't use power on the first lap of standing start
                {
                    if(vehicle.ERSController.normalizedCharge > 0.99)
                    {
                        SetErs(vehicle, ERSController.Mode.Hybrid);
                    }
                    else if (vehicle.ERSController.normalizedCharge < 0.96)
                    {
                        SetErs(vehicle, ERSController.Mode.Harvest);
                    }
                    return;
                }

                //If we don't have enough fuel to finish the race, immediatly use the hybrid mode
                if (lapLeft * GetFuelBurnRate(vehicle, Fuel.EngineMode.Low) > vehicle.performance.fuel.GetFuelLapsRemainingDecimal())
                {
                    SetErs(vehicle, ERSController.Mode.Hybrid);
                    return;
                }

                //If we barely have enough fuel, wait before using the hybride mode so that the power mode is available if needed
                if (vehicle.ERSController.normalizedCharge > 0.3 && lapLeft * GetFuelBurnRate(vehicle, Fuel.EngineMode.Medium) > vehicle.performance.fuel.GetFuelLapsRemainingDecimal())
                {
                    SetErs(vehicle, ERSController.Mode.Hybrid);
                    return;
                } 
                else
                {
                    if (vehicle.ERSController.isInHybridMode && (lapLeft) * GetFuelBurnRate(vehicle, Fuel.EngineMode.Medium) > vehicle.performance.fuel.GetFuelLapsRemainingDecimal())
                    {
                        return;
                    }
                }

            }

            if (powerEnabled && !((Game.instance.sessionManager.flag == SessionManager.Flag.SafetyCar || Game.instance.sessionManager.flag == SessionManager.Flag.VirtualSafetyCar)))
            {
                
                
                if (vehicle.timer.currentSector == Game.instance.sessionManager.yellowFlagSector && Game.instance.sessionManager.flag == SessionManager.Flag.Yellow)
                {
                    SetErs(vehicle, ERSController.Mode.Harvest);
                    return;
                }


                float minGap = Assistant.getMinGap(vehicle);

                Behaviour behaviour = SelectBehaviour(vehicle);


                

                if ( GetLapsRemainingDecimal(vehicle) < 1)
                {
                    int currentGate = vehicle.timer.lastActiveGateID;
                    int gateCount = Game.instance.sessionManager.GetAllGateTimers().Length; //Super dirty but idk where the gates ares
                    if ( (gateCount - currentGate - 1) < GetGatesRemaininginPowerMode(vehicle))
                    {
                        SetErs(vehicle, ERSController.Mode.Power);
                        return;
                    }
                }

                if (behaviour == Behaviour.Attack || behaviour == Behaviour.Both)
                {
                    //if we're stuck beheind a car we active power mode
                    // Let's say that 1% power =~ 0.01 secs
                    if (minGap < vehicle.ERSController.normalizedCharge)
                    {
                        SetErs(vehicle, ERSController.Mode.Power);
                        return;
                    }
                }
                else if (behaviour == Behaviour.Defend)
                {
                    //Only use power if we're about to be overtaken
                    if (GetGapBeheind(vehicle) * 1.5 < vehicle.ERSController.normalizedCharge)
                    {
                        SetErs(vehicle, ERSController.Mode.Power);
                        return;
                    }
                }

            }

            //If we're full on power we use it
            if (vehicle.ERSController.normalizedCharge > 0.90 && vehicle.ERSController.mode == ERSController.Mode.Harvest)
            {
                if (!options.doNotSpendExcessOnHybride && hybrideEnabled && lapLeft * GetFuelBurnRate(vehicle, Fuel.EngineMode.Overtake) > vehicle.performance.fuel.GetFuelLapsRemainingDecimal())
                {
                    SetErs(vehicle, ERSController.Mode.Hybrid);
                    return;
                }
                else if (!((Game.instance.sessionManager.flag == SessionManager.Flag.SafetyCar || Game.instance.sessionManager.flag == SessionManager.Flag.VirtualSafetyCar)))
                {
                    SetErs(vehicle, ERSController.Mode.Power);
                    return;
                }
            }
            //If power < 0.85 we go back to harvest mode in case we need the power to close a gap
            else if (vehicle.ERSController.mode != ERSController.Mode.Harvest && vehicle.ERSController.normalizedCharge < 0.85)
            {
                //Prevent switching to harvest mid overtake
                if (vehicle.ERSController.mode == ERSController.Mode.Hybrid || (vehicle.ERSController.mode == ERSController.Mode.Power && SelectBehaviour(vehicle) != Behaviour.Attack && SelectBehaviour(vehicle) != Behaviour.Both))
                {
                    SetErs(vehicle, ERSController.Mode.Harvest);
                    return;
                }
            }
        }

        internal static void SetErs(RacingVehicle vehicle, ERSController.Mode mode)
        {
            ERSController controller = vehicle.ERSController;
            if (controller.CanChangeToSpecificMode(mode) && controller.GetNormalizedCooldown(mode) == 0 && controller.mode != mode)
            {
                controller.SetERSMode(mode);
            }
        }

        internal static float getLapLeft(DriverAssistOptions options, RacingVehicle vehicle)
        {
            float lapLeft = GetLapsRemainingDecimal(vehicle);

            if (options.plannedPitstop && vehicle.championship.rules.isRefuelingOn)
            {
                int lap = vehicle.timer.lap;
                float num = vehicle.pathController.distanceAlongTrackPath01;
                if (num == 1f)
                {
                    num = 0f;
                }

                float diff = options.pitstopOnLap - (float)lap;

                lapLeft = diff - num;
                if (lapLeft < 0)
                {
                    lapLeft = 1 - num;
                }
            }
            else
            {
                lapLeft = lapLeft + options.fuel;
            }
            return lapLeft;
        }

        internal static float GetMechanicFuelBurnRate(RacingVehicle vehicle)
        {
            float mMechanicFuelBurnRate = 1f;

            if (vehicle.bonuses.IsBonusActive(MechanicBonus.Trait.FuelEconomy))
            {
                mMechanicFuelBurnRate = DesignDataManager.instance.GetDesignData().carPerformance.bonuses.lowFuelBurnRateBonus;
            }
            return mMechanicFuelBurnRate;
        }

        internal static float GetDriverFuelBurnRate(RacingVehicle vehicle)
        {
            float mDriverFuelBurnRate = 1f;
            if (vehicle.driver.personalityTraitController.HasSpecialCase(PersonalityTrait.SpecialCaseType.FuelBurnLow))
            {
                mDriverFuelBurnRate = DesignDataManager.instance.GetDesignData().carPerformance.bonuses.lowFuelBurnRateBonus;
            }
            else if (vehicle.driver.personalityTraitController.HasSpecialCase(PersonalityTrait.SpecialCaseType.FuelBurnHigh))
            {
                mDriverFuelBurnRate = DesignDataManager.instance.GetDesignData().carPerformance.bonuses.highFuelBurnRateBonus;
            }
            return mDriverFuelBurnRate;
        }

        internal static float GetChassisBonusModifier(RacingVehicle vehicle)
        {

            List<BonusChassisStats> activePartBonus = vehicle.car.GetActivePartBonus<BonusChassisStats>(vehicle);
            float mFuelChassisBonusModifier = 0f;
            for (int i = 0; i < activePartBonus.Count; i++)
            {
                if (activePartBonus[i].stat == CarChassisStats.Stats.FuelEfficiency)
                {
                    mFuelChassisBonusModifier += activePartBonus[i].bonusValue;
                }
            }
            return mFuelChassisBonusModifier;
        }

        internal static float getEnginemodeBurnRate(Fuel.EngineMode engineMode)
        {
            DesignData d = DesignDataManager.instance.GetDesignData();
            CarPerformanceDesignData mCarPerformance = d.carPerformance;
            float mEngineModeFuelBurnRate = 1f;

            switch (engineMode)
            {
                case Fuel.EngineMode.Low:
                    mEngineModeFuelBurnRate = mCarPerformance.fuel.lowBurnRate;
                    break;
                case Fuel.EngineMode.Medium:
                    mEngineModeFuelBurnRate = mCarPerformance.fuel.mediumBurnRate;
                    break;
                case Fuel.EngineMode.High:
                    mEngineModeFuelBurnRate = mCarPerformance.fuel.highBurnRate;
                    break;
                case Fuel.EngineMode.Overtake:
                    mEngineModeFuelBurnRate = mCarPerformance.fuel.overtakeBurnRate;
                    break;
                case Fuel.EngineMode.SuperOvertake:
                    mEngineModeFuelBurnRate = mCarPerformance.fuel.superOvertakeBurnRate;
                    break;
            }
            return mEngineModeFuelBurnRate;
        }



        internal static float GetFuelBurnRate(RacingVehicle vehicle, Fuel.EngineMode engineMode)
        {
            float mDriverFuelBurnRate = Assistant.GetDriverFuelBurnRate(vehicle);
            float mEngineModeFuelBurnRate = getEnginemodeBurnRate(engineMode);
            float mChassisFuelBurnRate = getChassisFuelBurnRate(vehicle);
            float mMechanicFuelBurnRate = GetMechanicFuelBurnRate(vehicle);


            float num = (mDriverFuelBurnRate + mEngineModeFuelBurnRate + mChassisFuelBurnRate + mMechanicFuelBurnRate) / 4f;
            /*
            if (vehicle.ERSController.isInHybridMode)
            {
                num = Mathf.Max(0f, num - DesignDataManager.instance.GetDesignData().ERSDesignData.hybridModeFuelSave);
            }
            */
            return num;
        }


        internal static float getChassisFuelBurnRate(RacingVehicle vehicle)
        {


            float mFuelChassisBonusModifier = GetChassisBonusModifier(vehicle);

            float stat = vehicle.car.ChassisStats.GetStat(CarChassisStats.Stats.FuelEfficiency, true, vehicle.car);
            float t = Mathf.Clamp01((stat + GameStatsConstants.chassisStatMax * mFuelChassisBonusModifier) / GameStatsConstants.chassisStatMax);
            float fuelEfficiencyChassisStatNegativeImpact = DesignDataManager.instance.GetDesignData().carChassis.fuelEfficiencyChassisStatNegativeImpact;
            float fuelEfficiencyChassisStatPositiveImpact = DesignDataManager.instance.GetDesignData().carChassis.fuelEfficiencyChassisStatPositiveImpact;

            return Mathf.Lerp(fuelEfficiencyChassisStatNegativeImpact, fuelEfficiencyChassisStatPositiveImpact, t);

        }

        internal static void AssistEngine(DriverAssistOptions options, RacingVehicle vehicle)
        {

            if (Game.instance.sessionManager.flag == SessionManager.Flag.SafetyCar || Game.instance.sessionManager.flag == SessionManager.Flag.VirtualSafetyCar)
            {
                vehicle.performance.fuel.SetEngineMode(Fuel.EngineMode.Low);
                return;
            }
            
            if (!options.engine || Game.instance.sessionManager.eventDetails.currentSession.sessionType != SessionDetails.SessionType.Race) return;

            var mode = Fuel.EngineMode.Medium;

            float fuelLapsRemainingDecimal = vehicle.performance.fuel.GetFuelLapsRemainingDecimal();
            float fuelLapDelta = vehicle.performance.fuel.GetTargetFuelLapDelta();

            float lapLeft = getLapLeft(options, vehicle);

            float superOvertakeConsum = GetFuelBurnRate(vehicle, Fuel.EngineMode.SuperOvertake);
            float overtakeConsum = GetFuelBurnRate(vehicle, Fuel.EngineMode.Overtake);
            float highConsum = GetFuelBurnRate(vehicle, Fuel.EngineMode.High);
            float medConsum = GetFuelBurnRate(vehicle, Fuel.EngineMode.Medium);
            float lowConsum = GetFuelBurnRate(vehicle, Fuel.EngineMode.Low);


            if (!options.smartEngine)
            {
                if (fuelLapsRemainingDecimal > lapLeft * superOvertakeConsum && vehicle.bonuses.activeMechanicBonuses.Contains(MechanicBonus.Trait.SuperOvertakeMode))
                {
                    mode = Fuel.EngineMode.SuperOvertake;

                }
                else if (fuelLapsRemainingDecimal > lapLeft * overtakeConsum)
                {
                    mode = Fuel.EngineMode.Overtake;
                }
                else if (fuelLapsRemainingDecimal > lapLeft * highConsum)
                {
                    mode = Fuel.EngineMode.High;

                }
                else if (fuelLapsRemainingDecimal > lapLeft * medConsum)
                {
                    mode = Fuel.EngineMode.Medium;
                }
                else
                {
                    mode = Fuel.EngineMode.Low;
                }
            }
            else
            {
                //If we don't have enough fuel we save it, if we have too much fuel we use it
                if (fuelLapsRemainingDecimal < (lapLeft + 0.02) * lowConsum)
                {
                    mode = Fuel.EngineMode.Low;
                }
                else if (fuelLapsRemainingDecimal < (lapLeft + 0.02) * medConsum)
                {
                    mode = Fuel.EngineMode.Medium;
                }
                else if ((fuelLapsRemainingDecimal > lapLeft * superOvertakeConsum || vehicle.ERSController.isInHybridMode) && vehicle.bonuses.activeMechanicBonuses.Contains(MechanicBonus.Trait.SuperOvertakeMode))
                {
                    mode = Fuel.EngineMode.SuperOvertake;

                }
                else if ((fuelLapsRemainingDecimal > lapLeft * overtakeConsum || vehicle.ERSController.isInHybridMode) && !vehicle.bonuses.activeMechanicBonuses.Contains(MechanicBonus.Trait.SuperOvertakeMode))
                {
                    mode = Fuel.EngineMode.Overtake;
                }
                else //Otherwise, burn fuel if we're fighting for a position and save it if we're not
                {
                    Behaviour behaviour = SelectBehaviour(vehicle);

                    if (behaviour == Behaviour.Save)
                    {
                        mode = Fuel.EngineMode.Low;
                    }
                    else if (behaviour == Behaviour.Attack || behaviour == Behaviour.Both)
                    {
                        if (vehicle.timer.currentSector == Game.instance.sessionManager.yellowFlagSector && Game.instance.sessionManager.flag == SessionManager.Flag.Yellow)
                        {
                            mode = Fuel.EngineMode.Low; //We can't fight if there's a yellow flag so we save fuel
                        }
                        else if (vehicle.bonuses.activeMechanicBonuses.Contains(MechanicBonus.Trait.SuperOvertakeMode))
                        {
                            mode = Fuel.EngineMode.SuperOvertake;
                        }
                        else
                        {
                            mode = Fuel.EngineMode.Overtake;
                        }
                    }
                    else
                    {
                        //Save more fuel than in non-smart mode in a super arbitrary manner
                        if (fuelLapsRemainingDecimal > lapLeft * overtakeConsum * 1.05 && vehicle.bonuses.activeMechanicBonuses.Contains(MechanicBonus.Trait.SuperOvertakeMode))
                        {
                            mode = Fuel.EngineMode.Overtake; //If we have super overtake, overtake can be used as a fuel saving mode
                        }
                        else if (fuelLapsRemainingDecimal > lapLeft * highConsum * 1.025)
                        {
                            mode = Fuel.EngineMode.High;

                        }
                        else if (fuelLapsRemainingDecimal > lapLeft * medConsum)
                        {
                            mode = Fuel.EngineMode.Medium;
                        }
                        else
                        {
                            mode = Fuel.EngineMode.Low;
                        }
                    }
                }

            }

            if (vehicle.car.seriesCurrentParts[1].partCondition.IsOnRed() && mode < Fuel.EngineMode.Medium)
            {
                mode = Fuel.EngineMode.Medium;
            }

            if (vehicle.timer.lap == 0 && options.boostEngine)
            {
                if (vehicle.bonuses.activeMechanicBonuses.Contains(MechanicBonus.Trait.SuperOvertakeMode))
                {
                    mode = Fuel.EngineMode.SuperOvertake;
                }
                else
                {

                    mode = Fuel.EngineMode.Overtake;
                }
            }


            vehicle.performance.fuel.SetEngineMode(mode);
        }

        public static float GetLapsRemainingDecimal(this RacingVehicle instance)
        {
            if (Main.IsEnduranceSeries(instance.championship.series))
            {
                return Mathf.Clamp01(1f - Game.instance.sessionManager.GetNormalizedSessionTime()) * Game.instance.sessionManager.duration / instance.performance.estimatedBestLapTime;
            }
            return (float)Game.instance.sessionManager.lapCount * Mathf.Clamp01(1f - instance.pathController.GetRaceDistanceTraveled01()) - (float)instance.GetLapsBehindLeader();
        }

        public static int GetLapsRemaining(this RacingVehicle instance)
        {
            return Mathf.FloorToInt(instance.GetLapsRemainingDecimal());
        }
    }



    [HarmonyPatch(typeof(SessionStrategy), "OnEnterGate")]
    static class SessionStrategy_OnEnterGate_Patch
    {
        static void Prefix(SessionStrategy __instance, RacingVehicle ___mVehicle, int inGateID, PathData.GateType inGateType)
        {
            if (!Main.enabled || inGateID % 2 != 0) return;

            var sessionType = Game.instance.sessionManager.eventDetails.currentSession.sessionType;
            var vehicle = ___mVehicle;

            var usesAI = vehicle.driver.personalityTraitController.UsesAIForStrategy(vehicle) || Game.instance.sessionManager.isUsingAIForPlayerDrivers;

            if (!Main.settings.aiControl && (sessionType == SessionDetails.SessionType.Race || sessionType == SessionDetails.SessionType.Practice))
            {
                if (usesAI || !vehicle.isPlayerDriver)
                    return;

                if (vehicle.pathState.IsInPitlaneArea() || vehicle.timer.hasSeenChequeredFlag)
                {

                    vehicle.performance.drivingStyle.SetDrivingStyle(DrivingStyle.Mode.BackUp);
                    vehicle.performance.fuel.SetEngineMode(Fuel.EngineMode.Low);
                    return;
                }

                DriverAssistOptions option = Main.settings.driver1AssistOptions;
                Assistant.TyreLog tyreLog = Assistant.tyre1;

                if (vehicle.carID == 1)
                {
                    option = Main.settings.driver2AssistOptions;
                    tyreLog = Assistant.tyre2;
                   
                }
                

                Assistant.AssistDrive(option, vehicle, tyreLog);
                Assistant.AssistERS(option, vehicle);
                Assistant.AssistEngine(option, vehicle);

                

                if (inGateID == 30)
                {
                    Assistant.NotifyPit(option, vehicle);
                }

            }

            if (Main.settings.practiceAssistOptions.enabled && sessionType == SessionDetails.SessionType.Practice
                && usesAI && vehicle.isPlayerDriver && vehicle.pathController.GetCurrentPath().pathType == PathController.PathType.Track)
            {
                if (vehicle.strategy.IsGoingToPit())
                    return;

                bool qualifyingBasedActive = vehicle.driver.contract.GetTeam().championship.rules.qualifyingBasedActive;
                int qualificationLevel = vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.QualifyingTrim).lastUnlockedLevel;
                int raceLevel = vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.RaceTrim).lastUnlockedLevel;
                var trim = vehicle.setup.currentSetup.trim;
                var needToPit = false;

                if (qualifyingBasedActive)
                {
                    if (Main.settings.practiceAssistOptions.knowledgePriority.qualification)
                    {
                        if (trim == SessionSetup.Trim.Qualifying && qualificationLevel == 3 && qualificationLevel != raceLevel)
                            needToPit = true;
                    }
                    if (Main.settings.practiceAssistOptions.knowledgePriority.race)
                    {
                        if (trim == SessionSetup.Trim.Race && raceLevel == 3 && qualificationLevel != raceLevel)
                            needToPit = true;
                    }
                }

                var compounds = vehicle.driver.contract.GetTeam().championship.rules.compoundsAvailable;
                int firstTyreLevel = vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.FirstOptionTyres).lastUnlockedLevel;
                int secondTyreLevel = vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.SecondOptionTyres).lastUnlockedLevel;
                int thirdTyreLevel = compounds == 2 ? 3 : vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.ThirdOptionTyres).lastUnlockedLevel;

                if (Main.settings.practiceAssistOptions.knowledgePriority.firstTyre)
                {
                    var firstTyreCompound = __instance.GetTyre(SessionStrategy.TyreOption.First, 0).GetCompound();
                    if (vehicle.setup.tyreSet.GetCompound() == firstTyreCompound && firstTyreLevel == 3
                        && (firstTyreLevel != secondTyreLevel || firstTyreLevel != thirdTyreLevel))
                        needToPit = true;
                }
                if (Main.settings.practiceAssistOptions.knowledgePriority.secondTyre)
                {
                    var secondTyreCompound = __instance.GetTyre(SessionStrategy.TyreOption.Second, 0).GetCompound();
                    if (vehicle.setup.tyreSet.GetCompound() == secondTyreCompound && secondTyreLevel == 3
                        && (secondTyreLevel != firstTyreLevel || secondTyreLevel != thirdTyreLevel))
                        needToPit = true;
                }
                if (Main.settings.practiceAssistOptions.knowledgePriority.thirdTyre && compounds == 3)
                {
                    var thirdTyreCompound = __instance.GetTyre(SessionStrategy.TyreOption.Third, 0).GetCompound();
                    if (vehicle.setup.tyreSet.GetCompound() == thirdTyreCompound && thirdTyreLevel == 3
                        && (thirdTyreLevel != firstTyreLevel || thirdTyreLevel != secondTyreLevel))
                        needToPit = true;
                }
                if (needToPit)
                {
                    __instance.ReturnToGarage();
                }
            }
        }
    }

    [HarmonyPatch(typeof(SessionSetup), "SetTargetTrim", new Type[] { })]
    static class SessionSetup_SetTargetTrim_Patch
    {
        public static void Postfix(SessionSetup __instance)
        {
            if (!Main.enabled || !Main.settings.practiceAssistOptions.enabled || !__instance.vehicle.isPlayerDriver
                || !(__instance.vehicle.driver.personalityTraitController.UsesAIForStrategy(__instance.vehicle) || Game.instance.sessionManager.isUsingAIForPlayerDrivers))
                return;

            var sessionType = Game.instance.sessionManager.eventDetails.currentSession.sessionType;

            switch (sessionType)
            {
                case SessionDetails.SessionType.Practice:
                    bool qualifyingBasedActive = __instance.vehicle.driver.contract.GetTeam().championship.rules.qualifyingBasedActive;
                    if (qualifyingBasedActive)
                    {
                        int qualificationLevel = 0;
                        int raceLevel = 0;
                        if (__instance.vehicle.practiceKnowledge.practiceReport != null)
                        {
                            qualificationLevel = __instance.vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.QualifyingTrim).lastUnlockedLevel;
                            raceLevel = __instance.vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.RaceTrim).lastUnlockedLevel;
                        }

                        if (Main.settings.practiceAssistOptions.knowledgePriority.qualification && qualificationLevel < 3 || raceLevel == 3)
                        {
                            __instance.sessionPitStop.currentSetup.trim = SessionSetup.Trim.Qualifying;
                            __instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Qualifying;
                            __instance.vehicle.practiceKnowledge.knowledgeType = PracticeReportSessionData.KnowledgeType.QualifyingTrim;
                        }
                        else
                        {
                            __instance.sessionPitStop.currentSetup.trim = SessionSetup.Trim.Race;
                            __instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Race;
                            __instance.vehicle.practiceKnowledge.knowledgeType = PracticeReportSessionData.KnowledgeType.RaceTrim;
                        }
                    }
                    else
                    {
                        __instance.sessionPitStop.currentSetup.trim = SessionSetup.Trim.Race;
                        __instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Race;
                        __instance.vehicle.practiceKnowledge.knowledgeType = PracticeReportSessionData.KnowledgeType.RaceTrim;
                    }

                    break;

                    //				case SessionDetails.SessionType.Qualifying:
                    //					__instance.sessionPitStop.currentSetup.trim = SessionSetup.Trim.Qualifying;
                    //					__instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Qualifying;
                    //					break;
                    //				
                    //				case SessionDetails.SessionType.Race:
                    //					__instance.sessionPitStop.currentSetup.trim = SessionSetup.Trim.Race;
                    //					__instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Race;
                    //					break;
            }
        }
    }

    [HarmonyPatch(typeof(SessionSetup), "SetTargetTrim", typeof(SessionSetup.Trim))]
    static class SessionSetup_SetTargetTrim2_Patch
    {
        static void Postfix(SessionSetup __instance, SessionSetup.Trim inTrim)
        {
            if (!Main.enabled || !Main.settings.practiceAssistOptions.enabled || !__instance.vehicle.isPlayerDriver
                || !(__instance.vehicle.driver.personalityTraitController.UsesAIForStrategy(__instance.vehicle) || Game.instance.sessionManager.isUsingAIForPlayerDrivers))
                return;

            var sessionType = Game.instance.sessionManager.eventDetails.currentSession.sessionType;
            switch (sessionType)
            {
                case SessionDetails.SessionType.Practice:
                    bool qualifyingBasedActive = __instance.vehicle.driver.contract.GetTeam().championship.rules.qualifyingBasedActive;
                    if (qualifyingBasedActive)
                    {
                        int qualificationLevel = 0;
                        int raceLevel = 0;
                        if (__instance.vehicle.practiceKnowledge.practiceReport != null)
                        {
                            qualificationLevel = __instance.vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.QualifyingTrim).lastUnlockedLevel;
                            raceLevel = __instance.vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.RaceTrim).lastUnlockedLevel;
                        }

                        if (Main.settings.practiceAssistOptions.knowledgePriority.qualification && qualificationLevel < 3 || raceLevel == 3)
                        {
                            __instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Qualifying;
                        }
                        else
                        {
                            __instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Race;
                        }
                    }
                    else
                    {
                        __instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Race;
                    }

                    break;
            }
        }
    }

    [HarmonyPatch(typeof(SessionStrategy), "GetSlickTyre")]
    static class SessionStrategy_GetSlickTyre_Patch
    {
        static bool Prefix(SessionStrategy __instance, ref TyreSet __result, RacingVehicle ___mVehicle, SessionDetails.SessionType inSessionType, float inNormalizedTime)
        {
            var vehicle = ___mVehicle;
            if (!Main.enabled || !Main.settings.practiceAssistOptions.enabled || !vehicle.isPlayerDriver || inSessionType != SessionDetails.SessionType.Practice)
                return true;

            var compounds = vehicle.driver.contract.GetTeam().championship.rules.compoundsAvailable;
            int firstTyreLevel = vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.FirstOptionTyres).lastUnlockedLevel;
            int secondTyreLevel = vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.SecondOptionTyres).lastUnlockedLevel;
            int thirdTyreLevel = compounds == 2 ? 3 : vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.ThirdOptionTyres).lastUnlockedLevel;

            var changeOn = SessionStrategy.TyreOption.First;
            var change = false;
            if (Main.settings.practiceAssistOptions.knowledgePriority.thirdTyre)
            {
                if (thirdTyreLevel < 3)
                {
                    changeOn = SessionStrategy.TyreOption.Third;
                    change = true;
                }
            }
            if (Main.settings.practiceAssistOptions.knowledgePriority.secondTyre)
            {
                if (secondTyreLevel < 3)
                {
                    changeOn = SessionStrategy.TyreOption.Second;
                    change = true;
                }
            }
            if (Main.settings.practiceAssistOptions.knowledgePriority.firstTyre)
            {
                if (firstTyreLevel < 3)
                {
                    changeOn = SessionStrategy.TyreOption.First;
                    change = true;
                }
            }
            if (!change)
            {
                if (firstTyreLevel < 3)
                    changeOn = SessionStrategy.TyreOption.First;
                else if (secondTyreLevel < 3)
                    changeOn = SessionStrategy.TyreOption.Second;
                else if (thirdTyreLevel < 3)
                    changeOn = SessionStrategy.TyreOption.Third;
            }

            var tyres = __instance.GetTyres(changeOn);
            foreach (var t in tyres)
            {
                if (t.GetCondition() > 0.4f)
                {
                    __result = t;
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(SessionStrategy), "DoesVehicleNeedToPit")]
    static class SessionStrategy_DoesVehicleNeedToPit_Patch
    {
        static bool Prefix(SessionStrategy __instance, ref bool __result, bool inIsPitlaneEntryGate, RacingVehicle ___mVehicle)
        {
            if (Main.enabled && Main.settings.aiControl && Main.settings.manualPitstop
                && ___mVehicle.isPlayerDriver && !Game.instance.sessionManager.IsSessionEnding())
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PersonalityTraitController_v2), "UsesAIForStrategy")]
    static class PersonalityTraitController_v2_UsesAIForStrategy_Patch
    {
        static bool Prefix(PersonalityTraitController_v2 __instance, ref bool __result, RacingVehicle inVehicle)
        {
            if (Main.enabled && Main.settings.aiControl)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SessionManager), "isUsingAIForPlayerDrivers", MethodType.Getter)]
    static class SessionManager_isUsingAIForPlayerDrivers_Patch
    {
        static bool Prefix(SessionManager __instance, ref bool __result)
        {
            if (Main.enabled && Main.settings.aiControl)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SessionStrategy), "OnSafetyCarEvent")]
    static class SessionStrategy_OnSafetyCarEvent_Patch
    {
        static bool Prefix(SessionStrategy __instance, RacingVehicle ___mVehicle)
        {
            if (Main.enabled && Main.settings.aiControl && Main.settings.manualPitstop
                && ___mVehicle.isPlayerDriver && !Game.instance.sessionManager.IsSessionEnding())
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SessionStrategy), "OnExitPitlane")]
    static class SessionStrategy_OnExitPitlane_Patch
    {
        static void Postfix(SessionStrategy __instance, RacingVehicle ___mVehicle)
        {
            var vehicle = ___mVehicle;
            if (Main.enabled && vehicle.isPlayerDriver && !Main.settings.aiControl
                && !Game.instance.sessionManager.IsSessionEnding() && Game.instance.sessionManager.sessionType == SessionDetails.SessionType.Race)
            {



                if (!(vehicle.strategy.previousStatus == SessionStrategy.Status.PitThruPenalty))
                {

                    if (vehicle.carID == 0 && Main.settings.driver1AssistOptions.engine && Main.settings.driver1AssistOptions.plannedPitstop)
                    {
                        var pitQueue = new Queue<int>(Main.settings.driver1AssistOptions.nextPitstops);
                        if (pitQueue.Count == 0)
                        {
                            Main.settings.driver1AssistOptions.plannedPitstop = false;
                            Main.settings.driver1AssistOptions.pitstopOnLap = 0;
                        }
                        else
                        {
                            Main.settings.driver1AssistOptions.pitstopOnLap = pitQueue.Dequeue();
                            Main.settings.driver1AssistOptions.nextPitstops = pitQueue.ToArray();
                        }
                    }
                    if (vehicle.carID == 1 && Main.settings.driver2AssistOptions.engine && Main.settings.driver2AssistOptions.plannedPitstop)
                    {
                        var pitQueue = new Queue<int>(Main.settings.driver2AssistOptions.nextPitstops);
                        if (pitQueue.Count == 0)
                        {
                            Main.settings.driver2AssistOptions.plannedPitstop = false;
                            Main.settings.driver2AssistOptions.pitstopOnLap = 0;
                        }
                        else
                        {
                            Main.settings.driver2AssistOptions.pitstopOnLap = pitQueue.Dequeue();
                            Main.settings.driver2AssistOptions.nextPitstops = pitQueue.ToArray();
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(GridPathState), "OnEnter")]
    static class GridPathState_OnEnter_Patch
    {
        static void Prefix(GridPathState __instance)
        {
            if (__instance.vehicle is RacingVehicle)
            {
                var vehicle = (RacingVehicle)__instance.vehicle;
                if (Main.enabled && vehicle.isPlayerDriver && !Main.settings.aiControl && vehicle.championship.rules.isRefuelingOn
                    && !Game.instance.sessionManager.IsSessionEnding() && Game.instance.sessionManager.sessionType == SessionDetails.SessionType.Race)
                {
                    /*var value = 0f;
                    if (Main.IsEnduranceSeries(vehicle.championship.series))
                    {
                        value = Mathf.RoundToInt(vehicle.timer.lap + vehicle.pathController.distanceAlongTrackPath01 + vehicle.performance.fuel.GetFuelLapsRemainingDecimal());
                    }
                    else
                    {
                        value = Mathf.RoundToInt(Game.instance.sessionManager.lapCount * vehicle.pathController.GetRaceDistanceTraveled01() + vehicle.performance.fuel.GetFuelLapsRemainingDecimal());
                    }

                    value = Mathf.Min(value, vehicle.GetLapsRemaining());*/

                    if (vehicle.carID == 0 && Main.settings.driver1AssistOptions.engine && Main.settings.driver1AssistOptions.plannedPitstop)
                    {
                        Main.settings.driver1AssistOptions.plannedPitstop = false;
                        Main.settings.driver1AssistOptions.pitstopOnLap = 0;
                    }
                    if (vehicle.carID == 1 && Main.settings.driver2AssistOptions.engine && Main.settings.driver2AssistOptions.plannedPitstop)
                    {
                        Main.settings.driver2AssistOptions.plannedPitstop = false;
                        Main.settings.driver2AssistOptions.pitstopOnLap = 0;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(SessionManager), "PrepareForSessionAfterLoad")]
    static class SessionManager_PrepareForSessionAfterLoad_Patch
    {
        static void Postfix(SessionManager __instance)
        {
            if (Main.enabled)
            {
                Main.settings.driver1AssistOptions.plannedPitstop = false;
                Main.settings.driver1AssistOptions.pitstopOnLap = 0;
                Main.settings.driver2AssistOptions.plannedPitstop = false;
                Main.settings.driver2AssistOptions.pitstopOnLap = 0;
            }
        }
    }
}