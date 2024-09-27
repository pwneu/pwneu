import requests  # type: ignore
import json
import argparse
import random
import concurrent.futures
from faker import Faker  # type: ignore

fake = Faker()

# Sample command: python seed_hint_usages.py --admin-password "PwneuPwneu!1"

def login_admin(api_url, admin_password):
    login_payload = {"userName": "admin", "password": admin_password}
    headers = {'Content-Type': 'application/json'}
    response = requests.post(f"{api_url}/identity/login", data=json.dumps(login_payload), headers=headers, verify=False)

    if response.status_code == 200:
        access_token = response.json().get('accessToken')
        print("Admin logged in successfully. Access token retrieved.")
        return access_token
    else:
        print(f"Failed to log in admin. Status code: {response.status_code}, Response: {response.text}")
        return None

def fetch_all_challenges(api_url, access_token):
    challenges = []
    page = 1
    has_next_page = True

    while has_next_page:
        response = requests.get(f"{api_url}/play/challenges?page={page}", headers={'Authorization': f'Bearer {access_token}'}, verify=False)
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

def add_hint_to_challenge(api_url, access_token, challenge_id):
    content = fake.sentence(nb_words=6)
    deduction = random.randint(1, 10) * 5
    hint_payload = {"content": content, "deduction": deduction}

    headers = {'Content-Type': 'application/json', 'Authorization': f'Bearer {access_token}'}
    response = requests.post(f"{api_url}/play/challenges/{challenge_id}/hints", data=json.dumps(hint_payload), headers=headers, verify=False)

    if response.status_code == 200:
        hint_id = response.text.strip('"')
        print(f"Hint added to challenge ID: {challenge_id}, Hint ID: {hint_id}")
        return hint_id
    else:
        print(f"Failed to add hint to challenge ID: {challenge_id}. Status code: {response.status_code}, Response: {response.text}")
        return None

def fetch_all_users(api_url, access_token):
    users = []
    page = 1
    has_next_page = True

    while has_next_page:
        response = requests.get(f"{api_url}/identity/users?page={page}", headers={'Authorization': f'Bearer {access_token}'}, verify=False)
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

def login_user(api_url, user_name, password):
    login_payload = {"userName": user_name, "password": password}
    headers = {'Content-Type': 'application/json'}
    response = requests.post(f"{api_url}/identity/login", data=json.dumps(login_payload), headers=headers, verify=False)

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

def use_hint(api_url, access_token, hint_id):
    headers = {'Authorization': f'Bearer {access_token}'}
    response = requests.post(f"{api_url}/play/hints/{hint_id}", headers=headers, verify=False)

    if response.status_code == 200:
        print(f"Hint {hint_id} used successfully.")
    else:
        print(f"Failed to use hint {hint_id}. Status code: {response.status_code}, Response: {response.text}")

def main():
    parser = argparse.ArgumentParser(description="Seed hints via API.")
    parser.add_argument("--admin-password", type=str, default="PwneuPwneu!1", help="Password for the admin user.")
    args = parser.parse_args()

    api_url = "https://localhost:37101"

    access_token = login_admin(api_url, args.admin_password)

    if access_token:
        challenges = fetch_all_challenges(api_url, access_token)

        hint_ids = []
        with concurrent.futures.ThreadPoolExecutor() as executor:
            future_to_hint = {executor.submit(add_hint_to_challenge, api_url, access_token, challenge['id']): challenge for challenge in challenges}
            for future in concurrent.futures.as_completed(future_to_hint):
                hint_id = future.result()
                if hint_id:
                    hint_ids.append(hint_id)

        print(f"Total hints added: {len(hint_ids)}")

        users = fetch_all_users(api_url, access_token)

        with concurrent.futures.ThreadPoolExecutor() as executor:
            for user in users:
                user_name = user['userName']
                user_access_token = login_user(api_url, user_name, args.admin_password)

                if user_access_token:
                    num_hints_to_use = random.randint(int(len(hint_ids) * 0.3), int(len(hint_ids) * 0.6))
                    hints_to_use = random.sample(hint_ids, k=num_hints_to_use)

                    for hint_id in hints_to_use:
                        executor.submit(use_hint, api_url, user_access_token, hint_id)

if __name__ == "__main__":
    main()
