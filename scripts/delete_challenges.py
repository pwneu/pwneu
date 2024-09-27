import requests  # type: ignore
import json
import argparse

# Sample command: python delete_categories.py --admin-password "PwneuPwneu!1"

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

def fetch_all_categories(api_url, access_token):
    headers = {'Authorization': f'Bearer {access_token}'}
    response = requests.get(f"{api_url}/play/categories/all", headers=headers, verify=False)

    if response.status_code == 200:
        categories = response.json()
        print(f"Total categories retrieved: {len(categories)}")
        return categories
    else:
        print(f"Failed to fetch categories. Status code: {response.status_code}, Response: {response.text}")
        return []

def delete_category(api_url, access_token, category_id):
    headers = {'Authorization': f'Bearer {access_token}'}
    response = requests.delete(f"{api_url}/play/categories/{category_id}", headers=headers, verify=False)

    if response.status_code == 204:
        print(f"Category with ID {category_id} deleted successfully.")
    else:
        print(f"Failed to delete category with ID {category_id}. Status code: {response.status_code}, Response: {response.text}")

def main():
    parser = argparse.ArgumentParser(description="Delete categories via API.")
    parser.add_argument("--admin-password", type=str, default="PwneuPwneu!1", help="Password for the admin user.")
    args = parser.parse_args()

    api_url = "https://localhost:37101"

    access_token = login_admin(api_url, args.admin_password)

    if access_token:
        categories = fetch_all_categories(api_url, access_token)

        for category in categories:
            category_id = category['id']
            delete_category(api_url, access_token, category_id)

if __name__ == "__main__":
    main()
