using Game.User;
using Game.Client.Network;
using UnityEngine;
using System.Threading.Tasks;
using System;

public class UserController : MonoBehaviour
{
    private UserService.UserServiceClient _userClient;

    async void Start()
    {
        await InitailizeAsync();
    }

    async Task InitailizeAsync()
    {
        _userClient = new UserService.UserServiceClient(GrpcConnection.channel);
    }

    public async Task<(bool success, string message, UserInfo userInfo)> RegisterAsync(string username, string password)
    {
        try
        {
            var response = await _userClient.RegisterAsync(new RegisterRequest
            {
                Username = username,
                Password = password
            });
            
            if (response.Result.Success)
            {
                return (true, response.Result.Message, response.UserInfo);
            }
            else
            {
                return (false, response.Result.Message, null);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    /// <summary>
    /// �г��� ����
    /// </summary>
    /// <param name="id"> Guid </param>
    /// <param name="newNickname"> �� �г��� </param>
    public async Task<(bool success, string message, string newNickname, bool exists)> UpdateNicknameAsync(string id, string newNickname)
    {
        try
        {
            var response = await _userClient.UpdateNicknameAsync(new UpdateNicknameRequest
            {
                Id = id,
                NewNickname = newNickname
            });

            return (response.Result.Success, response.Result.Message, response.NewNickname, response.Exists);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, string.Empty, false);
        }
    }
}
