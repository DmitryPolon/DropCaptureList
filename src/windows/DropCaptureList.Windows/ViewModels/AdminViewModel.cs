using System.Collections.ObjectModel;
using DropCaptureList.Windows.Helpers;
using DropCaptureList.Windows.Models;
using DropCaptureList.Windows.Services;

namespace DropCaptureList.Windows.ViewModels;

public sealed class AdminViewModel : ViewModelBase
{
    private readonly IIdentityService _identity;
    private string _newEmail = string.Empty;
    private string _newNickname = string.Empty;
    private string _newHousehold = string.Empty;
    private bool _newIsAppAdmin;
    private string _newHouseholdName = string.Empty;
    private string _removeHouseholdName = string.Empty;
    private AdminUserRow? _selectedUser;
    private string _statusMessage = "Full admin (reports, users) will live on the web app. This window is a temporary helper.";

    public AdminViewModel(IIdentityService identity)
    {
        _identity = identity;
        Users = new ObservableCollection<AdminUserRow>();
        AddUserCommand = new RelayCommand(AddUser);
        CreateHouseholdCommand = new RelayCommand(CreateHousehold);
        RemoveFromHouseholdCommand = new RelayCommand(RemoveFromHousehold);
        Reload();
    }

    public ObservableCollection<AdminUserRow> Users { get; }

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

    public string NewHousehold
    {
        get => _newHousehold;
        set => SetProperty(ref _newHousehold, value);
    }

    public bool NewIsAppAdmin
    {
        get => _newIsAppAdmin;
        set => SetProperty(ref _newIsAppAdmin, value);
    }

    public string NewHouseholdName
    {
        get => _newHouseholdName;
        set => SetProperty(ref _newHouseholdName, value);
    }

    public AdminUserRow? SelectedUser
    {
        get => _selectedUser;
        set => SetProperty(ref _selectedUser, value);
    }

    public string RemoveHouseholdName
    {
        get => _removeHouseholdName;
        set => SetProperty(ref _removeHouseholdName, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand AddUserCommand { get; }
    public RelayCommand CreateHouseholdCommand { get; }
    public RelayCommand RemoveFromHouseholdCommand { get; }

    private void Reload()
    {
        Users.Clear();
        foreach (var user in _identity.ListUsers())
        {
            Users.Add(user);
        }
    }

    private void AddUser()
    {
        try
        {
            _identity.AddUser(NewEmail, NewNickname, NewHousehold, NewNickname, NewIsAppAdmin);
            StatusMessage = "User added.";
            NewEmail = string.Empty;
            NewNickname = string.Empty;
            NewIsAppAdmin = false;
            Reload();
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
            _identity.CreateHousehold(NewHouseholdName);
            StatusMessage = "Household created.";
            NewHouseholdName = string.Empty;
            Reload();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void RemoveFromHousehold()
    {
        try
        {
            if (SelectedUser is null)
            {
                throw new InvalidOperationException("Select a user in the list.");
            }

            _identity.RemoveFromHousehold(SelectedUser.UserId, RemoveHouseholdName);
            StatusMessage = "Removed from household.";
            Reload();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
