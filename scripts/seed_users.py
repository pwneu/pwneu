import requests  # type: ignore
import json
import time
import argparse
from datetime import datetime, timedelta
from faker import Faker  # type: ignore

# Sample command: python seed_users.py --admin-password "PwneuPwneu!1" --users-count 10

def login_admin(api_url, admin_password):
    login_payload = {
        "userName": "admin",
        "password": admin_password
    }
    headers = {'Content-Type': 'application/json'}
    response = requests.post(f"{api_url}/identity/login", data=json.dumps(login_payload), headers=headers, verify=False)
    if response.status_code == 200:
        access_token = response.json().get('accessToken')
        print("Admin logged in successfully. Access token retrieved.")
        return access_token
    else:
        print(f"Failed to log in admin. Status code: {response.status_code}, Response: {response.text}")
        return None

def create_access_key(api_url, access_token):
    expiration_date = datetime.now() + timedelta(days=365)
    formatted_expiration = expiration_date.strftime("%Y-%m-%dT%H:%M:%S.%fZ")
    access_key_payload = {
        "forManager": False,
        "canBeReused": True,
        "expiration": formatted_expiration
    }
    headers = {
        'Content-Type': 'application/json',
        'Authorization': f'Bearer {access_token}'
    }
    response = requests.post(f"{api_url}/identity/keys", data=json.dumps(access_key_payload), headers=headers, verify=False)
    if response.status_code == 200:
        access_key_guid = response.text.strip().strip('"')
        print(f"Access key created successfully: {access_key_guid} (length: {len(access_key_guid)})")
        return access_key_guid
    else:
        print(f"Failed to create access key. Status code: {response.status_code}, Response: {response.text}")
        return None

def register_users(api_url, call_count, access_key_guid):
    timestamp = int(time.time())
    fake = Faker()
    for i in range(call_count):
        unique_username = f"{fake.user_name()}_{timestamp}_{i}"
        unique_email = fake.unique.email()
        unique_full_name = fake.name()
        payload = {
            "userName": unique_username,
            "email": unique_email,
            "password": "PwneuPwneu!1",
            "fullName": unique_full_name,
            "accessKey": access_key_guid
        }
        headers = {'Content-Type': 'application/json'}
        response = requests.post(f"{api_url}/identity/register", data=json.dumps(payload), headers=headers, verify=False)
        if response.status_code == 201:
            print(f"User {unique_username} registered successfully.")
        else:
            print(f"Failed to register user {unique_username}. Status code: {response.status_code}, Response: {response.text}")

def verify_users(api_url, access_token):
    headers = {
        'Content-Type': 'application/json',
        'Authorization': f'Bearer {access_token}'
    }
    while True:
        response = requests.get(f"{api_url}/identity/users?excludeVerified=true", headers=headers, verify=False)
        if response.status_code == 200:
            users = response.json().get('items', [])
            if not users:
                print("No unverified users found.")
                break
            for user in users:
                user_id = user.get('id')
                verify_response = requests.put(f"{api_url}/identity/users/{user_id}/verify", headers=headers, verify=False)
                if verify_response.status_code == 204:
                    print(f"User {user_id} verified successfully.")
                else:
                    print(f"Failed to verify user {user_id}. Status code: {verify_response.status_code}, Response: {verify_response.text}")
        else:
            print(f"Failed to retrieve unverified users. Status code: {response.status_code}, Response: {response.text}")
            break

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Register users via API.")
    parser.add_argument("--admin-password", type=str, default="PwneuPwneu!1", help="Password for the admin user.")
    parser.add_argument("--users-count", type=int, default=30, help="Number of users to register.")
    args = parser.parse_args()
    api_url = "https://localhost:37101"
    access_token = login_admin(api_url, args.admin_password)
    if access_token:
        access_key_guid = create_access_key(api_url, access_token)
        if access_key_guid:
            register_users(api_url, args.users_count, access_key_guid)
            verify_users(api_url, access_token)
