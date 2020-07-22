﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IncohearentWebServer.Data;
using IncohearentWebServer.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;

namespace IncohearentWebServer
{
    public static class ConnectedUser
    {
        public static List<string> Id = new List<string>();        
    }
  
    // GameHub - SignalR Hub
    public class GameHub : Hub
    {
        static RestApiService restApi { get; set; }
        public User GameMaster { get; set; }
        public string GMConnectionId { get; set; }      

        public GameHub()
        {
            restApi = new RestApiService();             // REST API za dohvat i transformaciju fraza
            GameMaster = new User();                    // GameMaster - tko je pokrenuo sesiju
            GMConnectionId = "";                        // ConnectionId od GameMastera          
        }

        //----Lobby----//

        public async Task JoinLobby(User user)
        {
            // Procedura za ulazak u Lobby:
            // Ako su svi na istom WiFi-ju, formira se grupa sa njihovom 
            // javnom IP adresom te se igraci dodaju u prikladnu grupu.
            // Ostali igrači dobivaju obavijest o novom igraču.

            await Groups.AddToGroupAsync(Context.ConnectionId, user.PublicAddress);
            Lobby lobby = new Lobby(user.PublicAddress, user.PrivateAddress, user.UserId, true);
            await Clients.Groups(user.PublicAddress).SendAsync("JoinLobby", user, lobby);           
        }

        public async Task LeaveLobby(User user)
        {
            // Procedura za izlazak iz Lobbyja:
            // Igrača koji želi izaći se miče iz grupe, a ostali dobivaju obavijest

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, user.PublicAddress);
            await Clients.Groups(user.PublicAddress).SendAsync("LeaveLobby", user);
        }

        //----Session----//

        public async Task StartGame(User user)
        {            
            // Onaj tko pokrene igru u Lobbyju postaje GameMaster

            await Clients.Groups(user.PublicAddress).SendAsync("StartGame", user);
        }

        public async Task ConnectSession(User user)
        {
            // Spajaju se igrači iz istog Lobbyja u novi Session

            await Groups.AddToGroupAsync(Context.ConnectionId, user.PublicAddress);            
            await Clients.Groups(user.PublicAddress).SendAsync("ConnectSession", user);
        }

        public async Task DisconnectSession(User user)
        {
            // Odspajanje igrača iz Sessiona - testirati

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, user.PublicAddress);
            await Clients.Groups(user.PublicAddress).SendAsync("DisconnectSession", user);            
        }
       
        //----Phrases----//

        public async Task GeneratePhrases(User user, User gm, PhoneticPhrases generated)
        {            
            // Procedura generiranja frazi

            // Generira se fraza po fraza. Igrač koji je GameMaster ima ulogu suca koji dobiva 
            // originalnu, generiranu frazu. Ostali igrači u Sessionu dobivaju fonetski ekvivalent.
            // Sudac (GameMaster) treba sam prosuditi tko je pobjednik u kojoj rundi (na osnovu
            // tko je točno odgovorio i bio najbrži). Ako se fraza nije generirala, onda se šalje
            // novi zahtjev prema REST API-ju
         
            if (gm.PrivateAddress == user.PrivateAddress)
            {
                GameMaster = user;
                GMConnectionId = Context.ConnectionId;
            }
            
            //System.Diagnostics.Debug.WriteLine(gm.Username);
            //System.Diagnostics.Debug.WriteLine(user.Username);
            //System.Diagnostics.Debug.WriteLine(Context.ConnectionId);

            generated = restApi.GeneratePhoneticEquivalents();
            if (!string.IsNullOrEmpty(generated.PhraseGenerated) && !string.IsNullOrEmpty(generated.PhrasePhonetic))
            {
                await Clients.GroupExcept(user.PublicAddress, GMConnectionId).SendAsync("PhrasesGenerated", generated.PhrasePhonetic);
                await Clients.Client(GMConnectionId).SendAsync("OriginalPhraseFetched", generated.PhraseGenerated);
            }
                           
            else
                await Clients.Groups(user.PublicAddress).SendAsync("PhrasesNotGenerated", generated);
        }       
       
        //----Events----//

        public override Task OnConnectedAsync()
        {
            // Provjerava spojene korisnike
            ConnectedUser.Id.Add(Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            // Prati tko se odspojio
            ConnectedUser.Id.Remove(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
