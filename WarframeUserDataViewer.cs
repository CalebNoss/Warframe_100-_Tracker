using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Text.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using Microsoft.VisualBasic.ApplicationServices;
using System.Text.RegularExpressions;


/// I need to get the data from the warframe api using the userID then write to file overwriting data in "WarframeUserDataViewer.json" (make new file if it does not exist)
/// I need to show an error if the userID is invalid and if there is an error getting from the warframe API....

// some background setup for GUI
Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);

// make input window and get user ID
inputForm loginWindow = new inputForm();
Application.Run(loginWindow);

Console.WriteLine($"I got the user ID of: {loginWindow.userInputID}");

// get user data from api
using HttpClient client = new HttpClient();
string warframeUserDataURL = $"https://api.warframe.com/cdn/getProfileViewingData.php?playerId={loginWindow.userInputID}";
string jsonUserDataAPI = await client.GetStringAsync(warframeUserDataURL);
File.WriteAllText("WarframeUserData.json", jsonUserDataAPI);














// Read the Enemy Codex Data
string jsonString = File.ReadAllText("EnemyCodexData.json");

// Deserialize the json file into CodexDataRoot
using FileStream openStreamEnemyCodex = File.OpenRead("EnemyCodexData.json");
CodexDataRoot? CodexDataResult = JsonSerializer.Deserialize<CodexDataRoot>(openStreamEnemyCodex);
if (CodexDataResult is null)
{
    throw new InvalidOperationException("Enemy codex data deserialized to null... gotta fix that somehow");
}
CodexDataRoot enemyDataRoot = CodexDataResult;
CodexRemainingScansRoot remainingScansFinalResult = new();

int placeholderInternalName = 0;
foreach (var enemyType in enemyDataRoot.Results)
{
    ComparedCodexValues currentEntry = new ComparedCodexValues();
    string currentEntryInternalName = "";
    if (enemyType.Value.General.InternalName != "")
    {
        currentEntryInternalName = enemyType.Value.General.InternalName;
    }
    else
    {
        currentEntryInternalName = placeholderInternalName.ToString();
        placeholderInternalName++;
    }
    currentEntry.displayName = enemyType.Key;
    currentEntry.totalScans = enemyType.Value.General.Scans;
    currentEntry.remainingScans = enemyType.Value.General.Scans;

    string uniqueInternalName = currentEntryInternalName;
    int uniqueIdentifier = 1;
    while (remainingScansFinalResult.scansDictionary.ContainsKey(uniqueInternalName))
    {
        uniqueInternalName = $"{currentEntryInternalName+uniqueIdentifier}";
        uniqueIdentifier++;
    }

    remainingScansFinalResult.scansDictionary.Add(uniqueInternalName, currentEntry);
}


// read challenge data
jsonString = File.ReadAllText("warframeExportAchievements.json");

// Deserialize the json file into ChallengeDataRoot
using FileStream openStreamChallengeData = File.OpenRead("warframeExportAchievements.json");
ChallengeDataRoot? ChallengeDataResult = JsonSerializer.Deserialize<ChallengeDataRoot>(openStreamChallengeData);
if (ChallengeDataResult is null)
{
    throw new InvalidOperationException("Challenge data deserialized to null... gotta fix that somehow");
}


// read user data
jsonString = File.ReadAllText("WarframeUserData.json");

using FileStream openStreamUserData = File.OpenRead("WarframeUserData.json");
UserDataRoot? UserDataResult = JsonSerializer.Deserialize<UserDataRoot>(openStreamUserData);
if (UserDataResult is null)
{
    throw new InvalidOperationException("User data deserialized to null... gotta fix that somehow");
}
UserDataRoot userData = UserDataResult;

foreach (var enemyType in userData.Stats.Scans)
{
    // here the internal name always exists
    ComparedCodexValues currentEntry = new ComparedCodexValues();
    string currentEntryInternalName = enemyType.type.Replace("/Avatars/","/");
    currentEntryInternalName = currentEntryInternalName.Replace("Avatar","");
    currentEntry.completedScans = enemyType.scans;

    if (remainingScansFinalResult.scansDictionary.TryGetValue(currentEntryInternalName, out var existingEntry))
    {
        existingEntry.completedScans = currentEntry.completedScans;
        existingEntry.remainingScans = existingEntry.totalScans - currentEntry.completedScans;
        existingEntry.wasMatched = true;
        if (existingEntry.remainingScans <= 0)
        {
            existingEntry.isComplete = true;
        }
    }
    else
    {
        if (remainingScansFinalResult.scansDictionary.TryGetValue(currentEntryInternalName + "Agent", out var newExistingEntry))
        {
            newExistingEntry.completedScans = currentEntry.completedScans;
            newExistingEntry.remainingScans = newExistingEntry.totalScans - currentEntry.completedScans;
            newExistingEntry.wasMatched = true;
            if (newExistingEntry.remainingScans <= 0)
            {
                newExistingEntry.isComplete = true;
            }
        }
        else
        {
            remainingScansFinalResult.scansDictionary.Add(currentEntryInternalName, currentEntry);
        }
    }
}

foreach (var currEntry in remainingScansFinalResult.scansDictionary)
{   // move to list for later viewing

    // only show matched entries        -       Don't know about the others because they are unmatched
    if (currEntry.Value.wasMatched)
    {
        if (!currEntry.Value.isComplete)
        {
            currEntry.Value.internalName = currEntry.Key;
            remainingScansFinalResult.scansList.Add(currEntry.Value);
        }
    }
    else
    {
        currEntry.Value.internalName = currEntry.Key;
        remainingScansFinalResult.mismatchedScansList.Add(currEntry.Value);
    }
}

List<String> challengePrefixes = new List<string>{  "Titles/",  "Shadowgrapher/"  };

foreach (var currChallenge in ChallengeDataResult.Results.ToList())
{   // remove prefixes from challenges
    foreach (var currPrefix in challengePrefixes)
    {
        if (currChallenge.Key.StartsWith(currPrefix))
        {
            if (ChallengeDataResult.Results.Remove(currChallenge.Key, out var newValue))
            {
                ChallengeDataResult.Results[currChallenge.Key.Substring(currPrefix.Length)] = newValue;
            }
        }
    }    
}

foreach (var currChallenge in userData.Results[0].ChallengeProgress)
{
    if (ChallengeDataResult.Results.TryGetValue(currChallenge.Name, out var existingEntry))
    {
        existingEntry.completedProgress = currChallenge.Progress;
        if (existingEntry.completedProgress >= existingEntry.requiredCount)
        {
            existingEntry.isCompleted = true;
        }
    }
    else
    {
        // insert entry if not calendar or season or Koumei, or Kahl, or Inaros quest, or Yareli stuff, or a riven challenge ("Randomized"), or the sentient cosmetics, or a juncture challenge
        // also ignore incarnon entries
        
        ChallengeRequirementData newChallenge = new();
        newChallenge.mainName = currChallenge.Name;
        newChallenge.completedProgress = currChallenge.Progress;
        if (!newChallenge.mainName.StartsWith("Calendar") &&
        !newChallenge.mainName.StartsWith("Season") &&
        !newChallenge.mainName.StartsWith("Koumei") &&
        !newChallenge.mainName.EndsWith("KahlChallenge") &&
        !newChallenge.mainName.StartsWith("Yareli") && 
        !newChallenge.mainName.StartsWith("Mummy") && 
        !newChallenge.mainName.StartsWith("Randomized") && 
        !newChallenge.mainName.StartsWith("SentEvo") && // armor & ephemera sentient evolving
        !newChallenge.mainName.StartsWith("AckAndBrunt") && 
        !newChallenge.mainName.StartsWith("Angstrum") && 
        !newChallenge.mainName.StartsWith("Anku") && 
        !newChallenge.mainName.StartsWith("Atomos") && 
        !newChallenge.mainName.StartsWith("Boar") && 
        !newChallenge.mainName.StartsWith("Boltor") && 
        !newChallenge.mainName.StartsWith("Braton") && 
        !newChallenge.mainName.StartsWith("Bronco") && 
        !newChallenge.mainName.StartsWith("Burston") && 
        !newChallenge.mainName.StartsWith("CeramicDagger") && 
        !newChallenge.mainName.StartsWith("Despair") && 
        !newChallenge.mainName.StartsWith("Dread") && 
        !newChallenge.mainName.StartsWith("DualIchor") && 
        !newChallenge.mainName.StartsWith("DualToxocyst") && 
        !newChallenge.mainName.StartsWith("EntFist") && 
        !newChallenge.mainName.StartsWith("EntratiWrist") && 
        !newChallenge.mainName.StartsWith("Furis") && 
        !newChallenge.mainName.StartsWith("Gammacor") && 
        !newChallenge.mainName.StartsWith("Gorgon") && 
        !newChallenge.mainName.StartsWith("Hate") && 
        !newChallenge.mainName.StartsWith("Lato") && 
        !newChallenge.mainName.StartsWith("Latron") && 
        !newChallenge.mainName.StartsWith("Lex") && 
        !newChallenge.mainName.StartsWith("Magistar") && 
        !newChallenge.mainName.StartsWith("Miter") && 
        !newChallenge.mainName.StartsWith("NamiSolo") && 
        !newChallenge.mainName.StartsWith("Okina") && 
        !newChallenge.mainName.StartsWith("Paris") && 
        !newChallenge.mainName.StartsWith("Soma") && 
        !newChallenge.mainName.StartsWith("Strun") && 
        !newChallenge.mainName.StartsWith("Stug") && 
        !newChallenge.mainName.StartsWith("Sybaris") && 
        !newChallenge.mainName.StartsWith("Torid") && 
        !newChallenge.mainName.StartsWith("Vasto") && 
        !newChallenge.mainName.StartsWith("VoidHeavy") && 
        !newChallenge.mainName.StartsWith("ZarimanDagger") && 
        !newChallenge.mainName.StartsWith("ZarimanPistol") && 
        !newChallenge.mainName.StartsWith("ZarimanSemi") && 
        !newChallenge.mainName.StartsWith("ZarimanShotgun") && 
        !newChallenge.mainName.StartsWith("ZarimanTonfa") && 
        !newChallenge.mainName.StartsWith("Zylok") && 
        !newChallenge.mainName.StartsWith("CJ") && // Ceres -> Jupiter
        !newChallenge.mainName.StartsWith("DJ") && // Deimos -> Jupiter
        !newChallenge.mainName.StartsWith("EM") && // Earth -> Mars
        !newChallenge.mainName.StartsWith("ES") && // Eris -> Sedna
        !newChallenge.mainName.StartsWith("EV") && // Earth -> Venus
        !newChallenge.mainName.StartsWith("JE") && // Jupiter -> Europa
        !newChallenge.mainName.StartsWith("JS") && // Jupiter -> Saturn
        !newChallenge.mainName.StartsWith("MC") && // Mars -> Ceres
        !newChallenge.mainName.StartsWith("MP") && // Mars -> Phobos
        !newChallenge.mainName.StartsWith("NP") && // Neptune -> Pluto 
        !newChallenge.mainName.StartsWith("PE") && // Pluto -> Eris
        !newChallenge.mainName.StartsWith("PS") && // Pluto -> Sedna
        !newChallenge.mainName.StartsWith("SU") && // Saturn -> Uranus 
        !newChallenge.mainName.StartsWith("UN") && // Uranus -> Neptune 
        !newChallenge.mainName.StartsWith("VM")    // Venus -> Mercury
        )
        {
            ChallengeDataResult.ChallengeRequirementList.Add(newChallenge);
        }
    }
}

foreach (var currEntry in ChallengeDataResult.Results)
{   // move to list for viewing
    // only move if not completed, so only incomplete challenges are shown
    if (!currEntry.Value.isCompleted)
    {
        currEntry.Value.mainName = currEntry.Key;
        ChallengeDataResult.ChallengeRequirementList.Add(currEntry.Value);
    }
}


// public List<MiscData> MiscInfoTab { get; set; } = new();

// TODO: move to viewable structure
ViewableDataStructure viewableDataStructure = new()
{
    weaponTab = userData.Stats.Weapons,
    enemyTab = userData.Stats.Enemies,
    abilityTab = userData.Stats.Abilities,
    ChallengeTab = ChallengeDataResult.ChallengeRequirementList,
    XPTab = userData.Results[0].LoadOutInventory.XPInfo,
    ScansTab = remainingScansFinalResult.scansList,
    mismatchedScansTab = remainingScansFinalResult.mismatchedScansList
};
foreach (var mission in userData.Results[0].Missions)
{
    MergedMissionData currMergedMissionData = new();
    currMergedMissionData.Completes = mission.Completes;
    currMergedMissionData.Tier = mission.Tier;
    currMergedMissionData.Tag = mission.Tag;

    int foundHighScore = -999;
    var foundMission = userData.Stats.Missions.Find(currEntry => currEntry.type == mission.Tag);
    if (foundMission != null)
    {
        foundHighScore = foundMission.highScore;
    }
    currMergedMissionData.highScore = foundHighScore;

    viewableDataStructure.MissionTab.Add(currMergedMissionData);
}
foreach (var race in userData.Stats.Races)
{
    RaceDataFlattened currRaceData = new();
    currRaceData.trackName = race.Key;
    currRaceData.highScore = race.Value.highScore;
    viewableDataStructure.RaceTab.Add(currRaceData);
}
viewableDataStructure.MiscInfoTab = compileMiscData(userData);

/*

Compile misc data from user data to one general list for viewing

*/
List<MiscData> compileMiscData(UserDataRoot userData)
{
    List<MiscData> currMiscDataList = new();
    var miscVariablesResults = userData.Results[0].GetType().GetProperties();
    var miscVariablesStats = userData.Stats.GetType().GetProperties();
    foreach (var miscVariable in miscVariablesResults)
    {
        if (miscVariable.PropertyType == typeof(int) ||
        miscVariable.PropertyType == typeof(float) ||
        miscVariable.PropertyType == typeof(string))
        {
            var variableValue = miscVariable.GetValue(userData.Results[0]);
            MiscData currMiscData = new();
            currMiscData.InfoType = miscVariable.Name;
            if (variableValue != null)
            {
                currMiscData.Value = variableValue;
            }
            currMiscDataList.Add(currMiscData);
        }
    }
    foreach (var miscVariable in miscVariablesStats)
    {
        if (miscVariable.PropertyType == typeof(int) ||
        miscVariable.PropertyType == typeof(float) ||
        miscVariable.PropertyType == typeof(string))
        {
            var variableValue = miscVariable.GetValue(userData.Stats);
            MiscData currMiscData = new();
            currMiscData.InfoType = miscVariable.Name;
            if (variableValue != null)
            {
                currMiscData.Value = variableValue;
            }
            currMiscDataList.Add(currMiscData);
        }
    }
    return currMiscDataList;
}


/// <summary>
/// Actual main to show stats
/// </summary>
Application.Run(new viewingForm(viewableDataStructure));





// Class for getting user id
public class inputForm : Form
{
    public string validIDPattern = @"^[a-z0-9]{24}$";
    public bool userInputWasValid = false;
    public string userInputID = "";

    private Label mainInstructions;
    private TextBox userInputBox;
    private Button tryUserInput;

    public inputForm()
    {
        Text = "User ID Input";
        Width = 1600;
        Height = 980;
        this.StartPosition = FormStartPosition.CenterScreen;

        // make text label
        mainInstructions = new Label();
        mainInstructions.Text = "You need to give your user ID for data to be fetched\ninstructions to obtain it are in the readme file\n\nOnce you have your 24 character userID, enter it here:";
        mainInstructions.Size = new Size(800, 200);
        mainInstructions.Location = new Point(400, 200);
        mainInstructions.TextAlign = ContentAlignment.MiddleCenter;

        // make text input
        userInputBox = new TextBox();
        userInputBox.Size = new Size(800, 200);
        userInputBox.Location = new Point(400, 450);
        userInputBox.TextAlign = HorizontalAlignment.Center;

        // make button
        tryUserInput = new Button();
        tryUserInput.Size = new Size(800, 200);
        tryUserInput.Location = new Point(400, 700);
        tryUserInput.Text = "View Account Information";
        tryUserInput.TextAlign = ContentAlignment.MiddleCenter;
        tryUserInput.Click += checkUserID;

        this.Controls.Add(mainInstructions);
        this.Controls.Add(userInputBox);
        this.Controls.Add(tryUserInput);
    }
    
    private void checkUserID(object Sender, EventArgs e)
    {
        userInputID = userInputBox.Text;
        userInputWasValid = Regex.IsMatch(userInputID, validIDPattern);
        if (userInputWasValid)
        {
            MessageBox.Show("UserID given, getting data then showing it!", "Success", MessageBoxButtons.OK);
            this.Close();
        }
        else
        {
            MessageBox.Show("This is not a possible User ID. Please check the readme file instructions and try again.", "Error", MessageBoxButtons.OK);
            userInputBox.Focus();
        }
    }
}




// Class for main window to display data
public class viewingForm : Form
{
    public ViewableDataStructure userData;

    public viewingForm(ViewableDataStructure viewableData)
    {
        Text = "UserStatsViewer";
        Width = 1600;
        Height = 980;
        userData = viewableData;
        BuildGroups();
    }

    private void BuildGroups()
    {
        var tabControl = new TabControl { Dock = DockStyle.Fill};
        Controls.Add(tabControl);

        AddGridTab(tabControl, "Misc Stats", userData.MiscInfoTab);
        AddGridTab(tabControl, "Weapons", userData.weaponTab);
        AddGridTab(tabControl, "Enemies", userData.enemyTab);
        AddGridTab(tabControl, "Abilities", userData.abilityTab);
        AddGridTab(tabControl, "Missions", userData.MissionTab);
        AddGridTab(tabControl, "Races", userData.RaceTab);
        AddGridTab(tabControl, "Challenges", userData.ChallengeTab);
        AddGridTab(tabControl, "XP Info", userData.XPTab);
        AddGridTab(tabControl, "Enemy Scans", userData.ScansTab);
        AddGridTab(tabControl, "Mismatched Enemy Scans", userData.mismatchedScansTab);
    }
    
    private void AddGridTab<T>(TabControl tabControl, string title, List<T> entries)
    {
        var thisGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = true,
            DataSource = new SortableBindingList<T>(entries),
            ReadOnly = true,
            AllowUserToAddRows = false
        };

        var thisPage = new TabPage(title);
        thisPage.Controls.Add(thisGrid);
        tabControl.TabPages.Add(thisPage);
    }
}


/// <summary>
/// Sortable version of lists for sorting values
/// </summary>
public class SortableBindingList<T> : BindingList<T>
{
    private bool _isSorted;
    private ListSortDirection _direction;
    private PropertyDescriptor? _sortProperty;

    public SortableBindingList(List<T> list) : base(list) { }

    protected override bool SupportsSortingCore => true;
    protected override bool IsSortedCore => _isSorted;
    protected override ListSortDirection SortDirectionCore => _direction;
    protected override PropertyDescriptor? SortPropertyCore => _sortProperty;

    protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection dir)
    {
        var items = (List<T>)Items;
        items.Sort((a, b) =>
        {
            int result = Comparer<object>.Default.Compare(prop.GetValue(a), prop.GetValue(b));
            return dir == ListSortDirection.Ascending ? result : -result;
        });

        _sortProperty = prop;
        _direction = dir;
        _isSorted = true;

        OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
    }

    protected override void RemoveSortCore()
    {
        _isSorted = false;
    }
}







/*

ENEMY CODEX SCAN REQUIREMENTS

*/
public class CodexDataRoot
{
    public Dictionary<string, EnemyData> Results { get; set; } = new();
}
public class EnemyData
{
    public GeneralizedData General { get; set; } = new();
}
public class GeneralizedData
{
    public string InternalName { get; set; } = "";
    public int Scans { get; set; } = -999;
}


/*

COMBINED CODEX CHECKLIST

*/
public class CodexRemainingScansRoot
{
    // key is internal name
    public Dictionary<string, ComparedCodexValues> scansDictionary { get; set; } = new();
    public List<ComparedCodexValues> scansList { get; set; } = new();
    public List<ComparedCodexValues> mismatchedScansList { get; set; } = new();
}

public class ComparedCodexValues
{
    public string internalName { get; set; } = "";
    public string displayName { get; set; } = "";
    public int totalScans { get; set; } = -999;
    public int completedScans { get; set; } = -999;
    public int remainingScans { get; set; } = 999;
    public bool isComplete { get; set; } = false;
    public bool wasMatched { get; set; } = false;
}


/*

USER DATA

*/
public class UserDataRoot
{
    public List<AccountResults> Results { get; set; } = new();
    public AccountStats Stats { get; set; } = new();
}

public class AccountResults
{
    public string DisplayName { get; set; } = "";
    public int PlayerLevel { get; set; } = -999;
    // List of Stalker marks
    public List<string> DeathMarks { get; set; } = new();
    // Zanuka marker
    public bool Harvestable { get; set; } = false;
    // Grustrag Three
    public bool DeathSquadable { get; set; } = false;
    public SomeInventoryData LoadOutInventory { get; set; } = new();
    public List<ChallengeData> ChallengeProgress { get; set; } = new();
    public List<AccountMissionData> Missions { get; set; } = new();
}
public class SomeInventoryData
{
    public List<XPData> XPInfo { get; set; } = new();
}
public class XPData
{
    public string ItemType { get; set; } = "";
    public int XP { get; set; } = -999;
}
public class ChallengeData
{
    public string Name { get; set; } = "";
    public int Progress { get; set; } = -999;
    
}
public class AccountMissionData
{
    public int Completes { get; set; } = -999;
    public int Tier { get; set; } = -999;
    public string Tag { get; set; } = "";
}
public class AccountStats
{
    public int MissionsCompleted { get; set; } = -999;
    public int MissionsQuit { get; set; } = -999;
    public int MissionsFailed { get; set; } = -999;
    public int MissionsDumped { get; set; } = -999;
    public int MissionsInterrupted { get; set; } = -999;
    public int PickupCount { get; set; } = -999;
    public int FishCount { get; set; } = -999;
    public int DestroyCount { get; set; } = -999;
    public int MeleeKills { get; set; } = -999;
    public int CiphersSolved { get; set; } = -999;
    public int CiphersFailed { get; set; } = -999;
    public float TimePlayedSec { get; set; } = -999;
    public float CipherTime { get; set; } = -999;
    public int Rating { get; set; } = -999;
    public int Rank { get; set; } = -999;
    public int Deaths { get; set; } = -999;
    public int Income { get; set; } = -999;
    public int ReviveCount { get; set; } = -999;
    public int PlayerLevel { get; set; } = -999;
    public int HealCount { get; set; } = -999;
    public int CaliberChicksScore { get; set; } = -999;
    public Dictionary<string, RaceData> Races { get; set; } = new();
    public List<ScanData> Scans { get; set; } = new();
    public List<StatsMissionData> Missions { get; set; } = new();
    public List<AbilityData> Abilities { get; set; } = new();
    public List<EnemyCombatData> Enemies { get; set; } = new();
    public List<WeaponCombatData> Weapons { get; set; } = new();
}
public class RaceData
{
    public int highScore { get; set; } = -999;
}
public class ScanData
{
    public int scans { get; set; } = -999;
    public string type { get; set; } = "";
}
public class StatsMissionData
{
    public int highScore { get; set; } = -999;
    public string type { get; set; } = "";
}
public class AbilityData
{
    public int used { get; set; } = -999;
    public string type { get; set; } = "";
}
public class EnemyCombatData
{
    public int kills { get; set; } = -999;
    public int headshots { get; set; } = -999;
    public int assists { get; set; } = -999;
    public int executions { get; set; } = -999;
    public int deaths { get; set; } = -999;
    public string type { get; set; } = "";
}
public class WeaponCombatData
{
    public int fired { get; set; } = -999;
    public int hits { get; set; } = -999;
    public int kills { get; set; } = -999;
    public int headshots { get; set; } = -999;
    public float equipTime { get; set; } = -999;
    public int xp { get; set; } = -999;
    public int assists { get; set; } = -999;
    public string type { get; set; } = "";
}

/*

DATA VIEWABLE STRUCTURE

*/
public class ViewableDataStructure
{
    public List<WeaponCombatData> weaponTab { get; set; } = new();
    public List<EnemyCombatData> enemyTab { get; set; } = new();
    public List<AbilityData> abilityTab { get; set; } = new();
    public List<MergedMissionData> MissionTab { get; set; } = new();
    public List<RaceDataFlattened> RaceTab { get; set; } = new();
    public List<ChallengeRequirementData> ChallengeTab { get; set; } = new();
    public List<XPData> XPTab { get; set; } = new();
    public List<MiscData> MiscInfoTab { get; set; } = new();
    public List<ComparedCodexValues> ScansTab { get; set; } = new();
    public List<ComparedCodexValues> mismatchedScansTab { get; set; } = new();
}



/*
    Challenge Data from GitHub public API thing to know required amounts for challenges
*/
public class ChallengeRequirementData
{
    public string mainName { get; set; } = "";
    
    public string uniqueName { get; set; } = "";
    public string name { get; set; } = "";
    public int requiredCount { get; set; } = 1;
    public int completedProgress { get; set; } = 0;
    public bool isCompleted { get; set; } = false;
    public bool hidden { get; set; } = false;
}

public class ChallengeDataRoot
{
    public Dictionary<string, ChallengeRequirementData> Results { get; set; } = new();
    public List<ChallengeRequirementData> ChallengeRequirementList { get; set; } = new();

}



public class MergedMissionData
{
    public int Completes { get; set; } = -999;
    public int Tier { get; set; } = -999;
    public string Tag { get; set; } = "";
    public int highScore { get; set; } = -999;
}
public class RaceDataFlattened
{
    public string trackName { get; set; } = "";
    public int highScore { get; set; } = -999;
}
public class MiscData
{
    public string InfoType { get; set; } = "";
    public object Value { get; set; } = new();
}