using DropCaptureList.Windows.Helpers;
using DropCaptureList.Windows.Models;
using DropCaptureList.Windows.Services;
using System.Collections.ObjectModel;

namespace DropCaptureList.Windows.ViewModels;

public sealed class AdminViewModel : ViewModelBase
{
    private readonly IIdentityService _identity;
    private readonly UserSession _session;
    private string _newEmail = string.Empty;
    private string _newNickname = string.Empty;
    private string _newHouseholdName = string.Empty;
    private string _newHouseholdMotto = string.Empty;
    private string _firstMemberEmail = string.Empty;
    private string _firstMemberNickname = string.Empty;
    private string _deleteHouseholdName = string.Empty;
    private string _mottoText = string.Empty;
    private MemberRow? _selectedMember;
    private LocalTenant? _selectedHousehold;
    private string _statusMessage = "Anyone in the household can add or remove members. Only an app admin can create or delete a household.";
    private bool _changed;

    public AdminViewModel(IIdentityService identity, UserSession session)
    {
        _identity = identity;
        _session = session;
        Members = new ObservableCollection<MemberRow>();
        Households = new ObservableCollection<LocalTenant>();
        AddMemberCommand = new RelayCommand(AddMember);
        CreateHouseholdCommand = new RelayCommand(CreateHousehold, () => _session.IsAppAdmin);
        AddFirstMemberCommand = new RelayCommand(AddFirstMember, () => _session.IsAppAdmin);
        DeleteHouseholdCommand = new RelayCommand(DeleteHousehold, () => _session.IsAppAdmin);
        SaveMottoCommand = new RelayCommand(SaveMotto);
        RemoveMemberCommand = new RelayCommand(RemoveMember);
        MottoText = string.Empty;
        ReloadMembers();
        ReloadHouseholds();
    }

    public ObservableCollection<MemberRow> Members { get; }

    public ObservableCollection<LocalTenant> Households { get; }

    public LocalTenant? SelectedHousehold
    {
        get => _selectedHousehold;
        set
        {
            if (Equals(_selectedHousehold, value))
            {
                return;
            }

            SetProperty(ref _selectedHousehold, value);
            RaisePropertyChanged(nameof(MembersHeading));
            ReloadMembers();
        }
    }

    public bool Changed => _changed;

    public bool IsAppAdmin => _session.IsAppAdmin;

    public string HouseholdName => _session.TenantName;

    public string MembersHeading => $"Emails in {MembersHousehold}";

    private string MembersHousehold =>
        string.IsNullOrWhiteSpace(SelectedHousehold?.Name) ? _session.TenantName : SelectedHousehold.Name;

    public string WebAppUrl => AdminSnapshot.WebAppUrl;

    public string NewEmail
    {
        get => _newEmail;
        set => SetProperty(ref _newEmail, value);
    }

    public string NewNickname
    {
        get => _newNickname;
        set => SetProperty(ref _newNickname, value);
    }

    public string NewHouseholdName
    {
        get => _newHouseholdName;
        set => SetProperty(ref _newHouseholdName, value);
    }

    public string NewHouseholdMotto
    {
        get => _newHouseholdMotto;
        set => SetProperty(ref _newHouseholdMotto, value);
    }

    public string FirstMemberEmail
    {
        get => _firstMemberEmail;
        set => SetProperty(ref _firstMemberEmail, value);
    }

    public string FirstMemberNickname
    {
        get => _firstMemberNickname;
        set => SetProperty(ref _firstMemberNickname, value);
    }

    public string DeleteHouseholdName
    {
        get => _deleteHouseholdName;
        set => SetProperty(ref _deleteHouseholdName, value);
    }

    public string MottoText
    {
        get => _mottoText;
        set => SetProperty(ref _mottoText, value);
    }

    public MemberRow? SelectedMember
    {
        get => _selectedMember;
        set => SetProperty(ref _selectedMember, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand AddMemberCommand { get; }
    public RelayCommand CreateHouseholdCommand { get; }
    public RelayCommand AddFirstMemberCommand { get; }
    public RelayCommand DeleteHouseholdCommand { get; }
    public RelayCommand SaveMottoCommand { get; }
    public RelayCommand RemoveMemberCommand { get; }

    private async void ReloadHouseholds()
    {
        if (!_session.IsAppAdmin)
        {
            return;
        }

        try
        {
            var houses = await Task.Run(() => _identity.ListAllHouseholds().ToList());
            Households.Clear();
            foreach (var house in houses)
            {
                Households.Add(house);
            }

            if (SelectedHousehold is null)
            {
                SelectedHousehold = Households.FirstOrDefault(h =>
                    string.Equals(h.Name, _session.TenantName, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async void ReloadMembers()
    {
        try
        {
            var house = MembersHousehold;
            var members = await Task.Run(() => _identity.ListMembers(house).ToList());
            Members.Clear();
            foreach (var member in members)
            {
                Members.Add(member);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void AddMember()
    {
        try
        {
            _identity.AddMember(MembersHousehold, NewEmail, NewNickname);
            StatusMessage = "Member added. They sign in with that email and this household name.";
            NewEmail = string.Empty;
            NewNickname = string.Empty;
            _changed = true;
            ReloadMembers();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void CreateHousehold()
    {
        try
        {
            _identity.CreateHousehold(NewHouseholdName, NewHouseholdMotto, FirstMemberEmail, "");
            StatusMessage = "Household created. That email can sign in with the household name.";
            NewHouseholdName = string.Empty;
            NewHouseholdMotto = string.Empty;
            FirstMemberEmail = string.Empty;
            _changed = true;
            ReloadHouseholds();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void AddFirstMember()
    {
        try
        {
            if (SelectedHousehold is null)
            {
                throw new InvalidOperationException("Select a household in the list.");
            }

            _identity.AddMember(SelectedHousehold.Name, FirstMemberEmail, "");
            StatusMessage = $"Added {FirstMemberEmail} to {SelectedHousehold.Name}.";
            FirstMemberEmail = string.Empty;
            _changed = true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void DeleteHousehold()
    {
        try
        {
            var name = SelectedHousehold?.Name ?? DeleteHouseholdName;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Select a household, or type its name.");
            }

            _identity.DeleteHousehold(name);
            StatusMessage = "Household deleted, including its list and members.";
            DeleteHouseholdName = string.Empty;
            SelectedHousehold = null;
            _changed = true;
            ReloadHouseholds();
            ReloadMembers();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void SaveMotto()
    {
        try
        {
            _identity.SetHouseholdMotto(_session.TenantName, MottoText);
            StatusMessage = string.IsNullOrWhiteSpace(MottoText) ? "Motto cleared." : "Motto saved.";
            _changed = true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void RemoveMember()
    {
        try
        {
            if (SelectedMember is null)
            {
                throw new InvalidOperationException("Select a member in the list.");
            }

            _identity.RemoveFromHousehold(SelectedMember.UserId, MembersHousehold);
            StatusMessage = "Removed from this household.";
            _changed = true;
            ReloadMembers();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
