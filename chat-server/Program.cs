﻿using System.Net;
using System.Net.Sockets;
using System.Text;

class Server {
    const int MAX_MESSAGE_LENGTH = 200;

    public class User {
        public Socket Connection;
        public string Name;
        public User(Socket connection, string name) {
            this.Connection = connection;
            this.Name = name;
        }
        public void Send(string data) {
            Connection.Send(Encoding.ASCII.GetBytes(data));
        }
    }

    private const int MAXLINE = 1024;
    private const int PORT = 9000;

    private Socket socket;
    public List<User> Users = new List<User>();
		private IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, PORT);

    public Server() {
        socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(ipEndPoint);
    }

    private string[] ParseNextRequest(Socket connection) {
        byte[] buffer = new byte[MAXLINE];
        connection.Receive(buffer); 
        string data = Encoding.ASCII.GetString(buffer);
        string[] fields = data.Split("|");
        fields[0] = fields[0].ToUpper();
        return fields;
    }

    private void HandleUser(object? obj) {
        if (obj == null) {
            throw new NullReferenceException("User should not be null");
        }
        User user = (User) obj;
        string[] fields = ParseNextRequest(user.Connection);
        string command = fields[0];
        while (true) {
            switch (command) {
                case "CONNECT": {
                    Console.WriteLine($"User [{user.Name}] is already connected");
                    user.Send($"REJECTED|{user.Name}|Already connected|");
                    break;
                }
                case "SAY": {
                    string message = fields[1];
                    if (message.Length > MAX_MESSAGE_LENGTH) {
                        user.Send($"REJECTED|{user.Name}|Message is too long|");
                        break;
                    }
                    Console.WriteLine($"[{user.Name}]  {message}");
                    foreach (User otherUser in Users) {
                        if (otherUser != user) {
                            otherUser.Send($"PUBLIC|{user.Name}|{message}|");
                        }
                    }
                    break;
                }
                case "EXIT": {
                    Console.WriteLine($"User [{user.Name}] left.");
                    Users.Remove(user);
                    foreach (User otherUser in Users) {
                        otherUser.Send($"LEFT|{user.Name}|");
                    }
                    user.Connection.Close();
                    return;
                }
                default: {
                    Console.WriteLine($"Unknown command {command}");
                    user.Send($"ERROR|{command}|");
                    break;
                }
            }
            fields = ParseNextRequest(user.Connection);
            command = fields[0];
        }
    }

    public void Start() {
        List<Thread> threads = new List<Thread>();
        socket.Listen();
        Console.WriteLine($"Server is listening on {ipEndPoint}");
        while (true) {
            try {
                Socket connection = socket.Accept();
                Console.WriteLine(
                    $"Accepted connection from {connection.RemoteEndPoint}");
                string[] fields = ParseNextRequest(connection);
                if (fields[0] == "CONNECT") {
                    string username = fields[1];
                    if (Users.Select((user) => user.Name).Contains(username)) {
                        Console.WriteLine(
                            $"Username [{username}] is already taken. " +
                            "Rejecting connection.");
                        string response = 
                            $"REJECTED|{username}|Name is Taken|";
                        connection.Send(Encoding.ASCII.GetBytes(response));
                        connection.Close();
                        continue;
                    }
                    User newUser = new User(connection, username);
                    Console.WriteLine($"New user [{username}] joined.");
                    newUser.Send($"CONNECTED|{username}|");
                    foreach (User user in Users) {
                        user.Send($"JOINED|{newUser.Name}|");
                    }
                    Users.Add(newUser);
                    Thread thread = 
                        new Thread(new ParameterizedThreadStart(HandleUser));
                    thread.Start(newUser);
                    threads.Add(thread);
                } else {
                    connection.Close();
                }
            } catch (Exception e) {
                Console.WriteLine(e);
                break;
            }
        }
        foreach (Thread thread in threads) {
            thread.Join();
        }
        socket.Close();
    }
}

class Program {

    static void Main(string[] args) {
        Server server = new Server();
        server.Start();
    }
}
