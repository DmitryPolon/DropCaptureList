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
    private string _statusMessage = "Anyone in the household can add or remove members. Only an app admin can create or delete a household.";
    private bool _changed;

    public AdminViewModel(IIdentityService identity, UserSession session)
    {
        _identity = identity;
        _session = session;
        Members = new ObservableCollection<MemberRow>();
        AddMemberCommand = new RelayCommand(AddMember);
        CreateHouseholdCommand = new RelayCommand(CreateHousehold, () => _session.IsAppAdmin);
        DeleteHouseholdCommand = new RelayCommand(DeleteHousehold, () => _session.IsAppAdmin);
        SaveMottoCommand = new RelayCommand(SaveMotto);
        RemoveMemberCommand = new RelayCommand(RemoveMember);
        MottoText = string.Empty;
        ReloadMembers();
    }

    public ObservableCollection<MemberRow> Members { get; }

    public bool Changed => _changed;

    public bool IsAppAdmin => _session.IsAppAdmin;

    public string HouseholdName => _session.TenantName;

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
    public RelayCommand DeleteHouseholdCommand { get; }
    public RelayCommand SaveMottoCommand { get; }
    public RelayCommand RemoveMemberCommand { get; }

    private async void ReloadMembers()
    {
        try
        {
            var members = await Task.Run(() => _identity.ListMembers(_session.TenantName).ToList());
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
            _identity.AddMember(_session.TenantName, NewEmail, NewNickname);
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
            _identity.CreateHousehold(NewHouseholdName, NewHouseholdMotto, FirstMemberEmail, FirstMemberNickname);
            StatusMessage = "Household created with the first member.";
            NewHouseholdName = string.Empty;
            NewHouseholdMotto = string.Empty;
            FirstMemberEmail = string.Empty;
            FirstMemberNickname = string.Empty;
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
            _identity.DeleteHousehold(DeleteHouseholdName);
            StatusMessage = "Household deleted, including its list and members.";
            DeleteHouseholdName = string.Empty;
            _changed = true;
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

            _identity.RemoveFromHousehold(SelectedMember.UserId, _session.TenantName);
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
