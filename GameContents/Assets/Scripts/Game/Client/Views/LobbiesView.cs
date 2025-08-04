using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Client.Controllers;
using Game.Lobbies;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using static Game.Client.LobbiesConstants;

namespace Game.Client.Views
{
    public class LobbiesView : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField] LobbiesController _controller;

        [Header("Canvas - LobbyList")]
        [SerializeField] Canvas _lobbyList;
        [SerializeField] Transform _lobbyListcontent;
        [SerializeField] Button _lobbyListRefreshList;
        [SerializeField] Button _lobbyListCreateLobby;
        [SerializeField] Button _lobbyListJoinLobby;
        LobbyInfoSlot _lobbyListSlot;
        Dictionary<int, LobbyInfoSlot> _lobbyListSlots;
        int _selectedLobbyId = -1;

        [Header("Canvas - CreateLobby")]
        [SerializeField] Canvas _createLobby;
        [SerializeField] TMP_InputField _createLobbyName;
        [SerializeField] TMP_InputField _createLobbyMaxClient;
        [SerializeField] Button _createLobbyConfirm;
        [SerializeField] Button _createLobbyCancel;

        [Header("Canvas - InLobby")]
        [SerializeField] Canvas _inLobby;
        [SerializeField] Transform _inLobbyContent;
        [SerializeField] Button _inLobbyReady;
        [SerializeField] Button _inLobbyPlay;
        UserInLobbyInfoSlot _userInLobbySlot;
        List<UserInLobbyInfoSlot> _userInLobbySlots;

        [Header("Canvas - Loading")]
        [SerializeField] Canvas _loading;

        [Header("UI (Canvas - Alert)")]
        [SerializeField] Canvas _alert;
        [SerializeField] TMP_Text _alertMessage;

        private void Start()
        {
            _lobbyListSlot = _lobbyListcontent.GetChild(0).GetComponent<LobbyInfoSlot>();
            _lobbyListSlot.gameObject.SetActive(false);
            _lobbyListSlots = new Dictionary<int, LobbyInfoSlot>(); // 로비목록은 수십개이상이 될수있음. random 한 lobbyId 로 인덱스 검색 용이하려면 Dictionary 이 나음

            _userInLobbySlot = _inLobbyContent.GetChild(0).GetComponent<UserInLobbyInfoSlot>();
            _lobbyListSlot.gameObject.SetActive(false);
            _userInLobbySlots = new List<UserInLobbyInfoSlot>(8); // 이 게임은 8명을 초과하는 인게임 컨텐츠가 없다. 이정도로 적은 데이터는 선형 O(N)탐색이 Hash O(1) 탐색보다 저렴하다.
        }

        private void OnEnable()
        {
            _lobbyListRefreshList.onClick.AddListener(RefreshLobbyList);
            _lobbyListJoinLobby.onClick.AddListener(OnLobbyListJoinLobbyButtonClicked);
            _lobbyListCreateLobby.onClick.AddListener(_createLobby.Show);
            _createLobbyCancel.onClick.AddListener(_createLobby.Hide);
            _createLobbyConfirm.onClick.AddListener(OnCreateLobbyConfirmButtonClicked);
        }

        private void OnDisable()
        {
            _lobbyListRefreshList.onClick.RemoveListener(RefreshLobbyList);
            _lobbyListJoinLobby.onClick.RemoveListener(OnLobbyListJoinLobbyButtonClicked);
            _lobbyListCreateLobby.onClick.RemoveListener(_createLobby.Show);
            _createLobbyCancel.onClick.RemoveListener(_createLobby.Hide);
            _createLobbyConfirm.onClick.RemoveListener(OnCreateLobbyConfirmButtonClicked);
        }

        private async void RefreshLobbyList()
        {
            _loading.Show();

            // 남아있던 슬롯 다 파괴
            // TODO : 풀링
            foreach (var id in _lobbyListSlots.Keys.ToList())
            {
                Destroy(_lobbyListSlots[id].gameObject);
                _lobbyListSlots.Remove(id);
            }

            var (success, lobbylist) = await _controller.GetLobbyListAsync();

            if (success)
            {
                foreach (var lobbyInfo in lobbylist)
                {
                    LobbyInfoSlot slot = Instantiate(_lobbyListSlot, _lobbyListcontent);
                    slot.Refresh(lobbyInfo);
                    slot.onSelectButtonClicked += OnSelectLobbyInfoSlot;
                    slot.gameObject.SetActive(true);
                    _lobbyListSlots[lobbyInfo.LobbyId] = slot;
                }
            }

            _loading.Hide();
            _selectedLobbyId = -1;
            _lobbyListJoinLobby.interactable = false; // 새로 리스트 갱신됐으면 들어가려는 로비는 다시 선택해야함.
        }

        private void OnSelectLobbyInfoSlot(int lobbyId)
        {
            // 전에 선택한 로비 선택해제
            if (_selectedLobbyId >= 0)
            {
                if (_lobbyListSlots.TryGetValue(_selectedLobbyId, out LobbyInfoSlot prevslot))
                {
                    prevslot.Deselect();
                }
            }

            _lobbyListSlots[lobbyId].Select();
            _selectedLobbyId = lobbyId;
            _lobbyListJoinLobby.interactable = true; // 선택된 로비가 있어야 Join 클릭가능
        }

        private async void OnLobbyListJoinLobbyButtonClicked()
        {
            if (_selectedLobbyId < 0)
            {
                RefreshLobbyList();
                // TODO : 선택된 로비 없음 알림창 띄우기
                return;
            }

            _loading.Show();

            var (success, message, userInfos) = await _controller.JoinLobbyAsync(_selectedLobbyId);

            if (success)
            {
                _lobbyList.Hide(); // TODO : Canvas 만 숨길게 아니라, View 컴포넌트 비활성화도 해야함.
                _inLobby.Show();
                RefreshUserInLobbyContent(userInfos);
            }
            else
            {
                _alertMessage.text = "Failed to join lobby.";
                _alert.Show();
                await Task.Delay(2000);
                _alert.Hide();
            }

            _loading.Hide();
        }

        private void RefreshUserInLobbyContent(IList<UserInLobbyInfo> infos)
        {
            // 기존슬롯 다 파괴
            // TODO : 풀링 시스템
            foreach (var slot in _userInLobbySlots.ToList())
            {
                Destroy(slot.gameObject);
                _userInLobbySlots.Remove(slot);
            }

            foreach (var info in infos)
            {
                var slot = Instantiate(_userInLobbySlot, _inLobbyContent);
                slot.Refresh(info);
                slot.gameObject.SetActive(true);
                _userInLobbySlots.Add(slot);
            }
        }

        private async void OnCreateLobbyConfirmButtonClicked()
        {
            _loading.Show();

            int maxClient = int.Parse(_createLobbyMaxClient.text);
            var (success, message) = await _controller.CreateLobbyAsync(maxClient);

            if (success)
            {
                await _controller.SetLobbyCustomPropertiesAsync(new Dictionary<string, string>
                {
                    { LOBBY_NAME, _createLobbyName.text }
                });

                _createLobby.Hide();
                _lobbyList.Hide();
                _inLobby.Show();
            }

            _loading.Hide();

            _alertMessage.text = message;
            _alert.Show();
            await Task.Delay(2000);
            _alert.Hide();
        }
    }
}
