import requests  # type: ignore
import json
import argparse

# Sample command: python delete_members.py --admin-password "PwneuPwneu!1" --api-url "http://localhost:37100"

def login_admin(api_url, admin_password):
    login_payload = {"userName": "admin", "password": admin_password}
    headers = {'Content-Type': 'application/json'}
    response = requests.post(f"{api_url}/identity/login", data=json.dumps(login_payload), headers=headers)

    if response.status_code == 200:
        access_token = response.json().get('accessToken')
        print("Admin logged in successfully. Access token retrieved.")
        return access_token
    else:
        print(f"Failed to log in admin. Status code: {response.status_code}, Response: {response.text}")
        return None


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


def check_user_role(api_url, user):
    user_name = user['userName']
    login_payload = {"userName": user_name, "password": "PwneuPwneu!1"}  
    headers = {'Content-Type': 'application/json'}

    response = requests.post(f"{api_url}/identity/login", data=json.dumps(login_payload), headers=headers)

    if response.status_code == 200:
        user_info = response.json()
        if "Member" in user_info.get("roles", []):
            return user['id']  
    else:
        print(f"Failed to log in user '{user_name}'. Status code: {response.status_code}, Response: {response.text}")
    return None


def delete_user(api_url, access_token, user_id):
    headers = {'Authorization': f'Bearer {access_token}'}
    response = requests.delete(f"{api_url}/identity/users/{user_id}", headers=headers)

    if response.status_code == 204:
        print(f"User with ID {user_id} deleted successfully.")
    else:
        print(f"Failed to delete user with ID {user_id}. Status code: {response.status_code}, Response: {response.text}")


def main():
    parser = argparse.ArgumentParser(description="Delete members via API.")
    parser.add_argument("--admin-password", type=str, default="PwneuPwneu!1", help="Password for the admin user.")
    parser.add_argument("--api-url", type=str, default="http://localhost:37100/api/v1", help="Base URL for the API.")
    args = parser.parse_args()

    api_url = args.api_url

    access_token = login_admin(api_url, args.admin_password)

    if access_token:
        users = fetch_all_users(api_url, access_token)

        for user in users:
            user_id = check_user_role(api_url, user)
            if user_id:
                delete_user(api_url, access_token, user_id)


if __name__ == "__main__":
    main()
