import requests  # type: ignore
import json
import argparse
import random
import concurrent.futures

# Sample command: python seed_leaderboards.py --admin-password "PwneuPwneu!1" --api-url "http://localhost:37100"

def login_admin(api_url, admin_password):
    login_payload = {
        "userName": "admin",
        "password": admin_password
    }
    headers = {'Content-Type': 'application/json'}
    response = requests.post(f"{api_url}/identity/login", data=json.dumps(login_payload), headers=headers)

    if response.status_code == 200:
        access_token = response.json().get('accessToken')
        print("Admin logged in successfully. Access token retrieved.")
        return access_token
    else:
        print(f"Failed to log in admin. Status code: {response.status_code}, Response: {response.text}")
        return None


def allow_submissions(api_url, access_token):
    allow_payload = {"allowed": True}
    headers = {'Content-Type': 'application/json', 'Authorization': f'Bearer {access_token}'}
    response = requests.put(f"{api_url}/play/configurations/submissionsAllowed/allow", data=json.dumps(allow_payload), headers=headers)

    if response.status_code == 204:
        print("Submissions allowed successfully.")
    else:
        print(f"Failed to allow submissions. Status code: {response.status_code}, Response: {response.text}")


def fetch_all_challenges(api_url, access_token):
    challenges = []
    page = 1
    has_next_page = True

    while has_next_page:
        response = requests.get(f"{api_url}/play/challenges?page={page}&pageSize=20", headers={'Authorization': f'Bearer {access_token}'})
        if response.status_code == 200:
            data = response.json()
            challenges.extend(data['items'])
            has_next_page = data['hasNextPage']
            page += 1
        else:
            print(f"Failed to fetch challenges. Status code: {response.status_code}, Response: {response.text}")
            break


    print(f"Total challenges retrieved: {len(challenges)}")
    return challenges


def fetch_all_users(api_url, access_token):
    users = []
    page = 1
    has_next_page = True

    while has_next_page:
        response = requests.get(f"{api_url}/identity/users?page={page}", headers={'Authorization': f'Bearer {access_token}'})
        if response.status_code == 200:
            data = response.json()
            users.extend(data['items'])
            has_next_page = data['hasNextPage']
            page += 1
        else:
            print(f"Failed to fetch users. Status code: {response.status_code}, Response: {response.text}")
            break

    print(f"Total users retrieved: {len(users)}")
    return users


def login_user(api_url, user_name):
    login_payload = {
        "userName": user_name,
        "password": "PwneuPwneu!1"
    }
    headers = {'Content-Type': 'application/json'}
    response = requests.post(f"{api_url}/identity/login", data=json.dumps(login_payload), headers=headers)

    if response.status_code == 200:
        user_info = response.json()
        if "Member" in user_info.get("roles", []):
            print(f"User '{user_name}' logged in successfully.")
            return user_info['accessToken']
        else:
            print(f"User '{user_name}' does not have 'Member' role. Skipping.")
            return None
    else:
        print(f"Failed to log in user '{user_name}'. Status code: {response.status_code}, Response: {response.text}")
        return None


def submit_flag(api_url, access_token, challenge_id, flag):
    response = requests.post(f"{api_url}/play/challenges/{challenge_id}/submit?flag={flag}", headers={'Authorization': f'Bearer {access_token}'})
    response_text = response.text.strip('"')
    
    if response.status_code == 200:
        print(f"Flag '{flag}' submitted successfully for challenge ID: {challenge_id} with response: {response_text}.")
    else:
        print(f"Failed to submit flag '{flag}' for challenge ID: {challenge_id}. Status code: {response.status_code}, Response: {response_text}.")

    return response_text


def process_user_submission(api_url, user_access_token, challenges):
    for challenge in challenges:
        challenge_id = challenge['id']

        incorrect_attempts = random.randint(1, 3)

        for _ in range(incorrect_attempts):
            incorrect_flag = "INCORRECT_FLAG"  
            submit_flag(api_url, user_access_token, challenge_id, incorrect_flag)

        correct_flag = "PWNEU{PWNEU}"
        submit_flag(api_url, user_access_token, challenge_id, correct_flag)


def get_user_access_tokens(api_url, users):
    tokens = {}
    with concurrent.futures.ThreadPoolExecutor() as executor:
        futures = {executor.submit(login_user, api_url, user['userName']): user['userName'] for user in users}

        for future in concurrent.futures.as_completed(futures):
            user_name = futures[future]
            user_access_token = future.result()
            if user_access_token:
                tokens[user_name] = user_access_token
    return tokens


def main():
    parser = argparse.ArgumentParser(description="Seed submissions via API.")
    parser.add_argument("--admin-password", type=str, default="PwneuPwneu!1", help="Password for the admin user.")
    parser.add_argument("--api-url", type=str, default="http://localhost:37100/api/v1", help="Base URL of the API.")
    args = parser.parse_args()

    access_token = login_admin(args.api_url, args.admin_password)

    if access_token:
        allow_submissions(args.api_url, access_token)
        challenges = fetch_all_challenges(args.api_url, access_token)
        users = fetch_all_users(args.api_url, access_token)

        user_tokens = get_user_access_tokens(args.api_url, users)

        with concurrent.futures.ThreadPoolExecutor(max_workers=1000) as executor:
            futures = []
            for user_access_token in user_tokens.values():
                total_challenges = len(challenges)
                min_challenges_to_submit = max(int(total_challenges * 0.7), 1)
                max_challenges_to_submit = total_challenges
                num_challenges_to_submit = random.randint(min_challenges_to_submit, max_challenges_to_submit)
                challenges_to_submit = random.sample(challenges, k=num_challenges_to_submit)

                futures.append(executor.submit(process_user_submission, args.api_url, user_access_token, challenges_to_submit))

            for future in concurrent.futures.as_completed(futures):
                future.result()


if __name__ == "__main__":
    main()
