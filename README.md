# PWNEU Backend Server

The official backend server of PWNEU.

### 📚 Prerequisites

- [Git](https://git-scm.com)
- [.NET 8](https://dotnet.microsoft.com/download)
- [Docker Compose](https://www.docker.com)
- [Google App Password](https://support.google.com/accounts/answer/185833)

### 🚀 Getting Started (For development only)

- Clone the repository.

```sh
git clone https://github.com/pwneu/pwneu.git
cd pwneu
```

- If on windows, make sure the line endings of `wait-for-it.sh` script is `LF`.

```sh
(Get-Content -Raw -Path "./scripts/wait-for-it.sh") -replace "`r`n", "`n" | Set-Content -Path "./scripts/wait-for-it.sh"
```

- Generate `.env` file the using `.env.example` file.

```sh
cp .env.example .env # On Linux
Copy-Item .env.example .env # On Windows
```

- Make sure to fill this up using the email address and the app password.

```
SMTP_SENDER_ADDRESS=
SMTP_SENDER_PASSWORD=
```

- Or you can add a fake sender and disable emailing.

```
SMTP_SENDER_ADDRESS=pwneu@pwneu.pwneu
SMTP_SENDER_PASSWORD=pwneupwneupwneu
SMTP_NOTIFY_LOGIN_IS_ENABLED=false
SMTP_SEND_EMAIL_CONFIRMATION_IS_ENABLED=false
SMTP_SEND_PASSWORD_RESET_TOKEN_IS_ENABLED=false
```

- Build and run the container

```sh
docker-compose build
docker-compose up -d
```

### 📜 License
Before using our program, please refer to our [License](https://github.com/pwneu/pwneu/blob/main/LICENSE).

### 📚 External Resources
Resources I used to develop the server.

- [Vertical Slice Architecture](https://www.youtube.com/watch?v=msjnfdeDCmo)
- [Docker Compose ASP.NET Core](https://www.youtube.com/watch?v=WQFx2m5Ub9M)
- [Pagination](https://www.youtube.com/watch?v=X8zRvXbirMU)
- [RabbitMQ with MassTransit](https://www.youtube.com/watch?v=MzC0PgYocmk)
- [Result Pattern](https://www.youtube.com/watch?v=WCCkEe_Hy2Y)
- [Fusion Cache](https://www.youtube.com/watch?v=wGKSNqxN4KE)
- [Integration Testing](https://www.youtube.com/watch?v=tj5ZCtvgXKY)
- [Prometheus and Grafana](https://www.youtube.com/watch?v=ePYQEl_ZxCs)
- [Semantic Kernel](https://www.youtube.com/watch?v=f_hqGlt_2E8)

### 🙏 Thanks!
- Thank you for using our program (〃￣ ω ￣〃).
