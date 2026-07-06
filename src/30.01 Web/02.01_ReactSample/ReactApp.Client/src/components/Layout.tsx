import { NavLink, Outlet } from "react-router-dom";
import { useMsal, useIsAuthenticated } from "@azure/msal-react";
import {
  FluentProvider,
  makeStyles,
  tokens,
  Button,
  Text,
  Divider,
} from "@fluentui/react-components";
import {
  Bot24Regular,
  SignOut24Regular,
} from "@fluentui/react-icons";
import { getLoginRequest, getUserName } from "../auth/authConfig";
import { appTheme } from "../theme/appTheme";
import AboutDialog from "./AboutDialog";

const useStyles = makeStyles({
  root: {
    display: "flex",
    height: "100vh",
    backgroundColor: tokens.colorNeutralBackground2,
  },
  sidebar: {
    width: "240px",
    backgroundColor: tokens.colorNeutralBackground1,
    display: "flex",
    flexDirection: "column",
    borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
    flexShrink: 0,
  },
  sidebarHeader: {
    padding: "16px",
    display: "flex",
    alignItems: "center",
    gap: "8px",
    color: tokens.colorBrandForeground1,
  },
  navList: {
    listStyle: "none",
    padding: "0",
    margin: "0",
    flex: 1,
  },
  navItem: {
    display: "flex",
    alignItems: "center",
    gap: "10px",
    padding: "10px 16px",
    textDecoration: "none",
    color: tokens.colorNeutralForeground1,
    fontSize: "14px",
    "&:hover": {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  navItemActive: {
    backgroundColor: tokens.colorBrandBackground2,
    color: tokens.colorBrandForeground1,
    fontWeight: 600,
  },
  sidebarFooter: {
    padding: "12px 16px",
  },
  main: {
    flex: 1,
    display: "flex",
    flexDirection: "column",
    overflow: "hidden",
  },
  header: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    padding: "12px 24px",
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  content: {
    flex: 1,
    padding: "24px",
    overflow: "auto",
  },
  headerRight: {
    display: "flex",
    alignItems: "center",
    gap: "8px",
  },
});

const navItems = [
  { to: "/agent", label: "Agent", icon: <Bot24Regular /> },
];

export default function Layout() {
  const styles = useStyles();
  const { instance, accounts } = useMsal();
  const isAuthenticated = useIsAuthenticated();

  const handleLogin = () => instance.loginRedirect(getLoginRequest());
  const handleLogout = async () => {
    const account = instance.getActiveAccount() ?? accounts[0] ?? null;
    await instance.logoutRedirect({ account: account ?? undefined });
  };

  const userName = getUserName(accounts[0]);

  return (
    <FluentProvider theme={appTheme}>
      <div className={styles.root}>
        <nav className={styles.sidebar}>
          <div className={styles.sidebarHeader}>
            <Bot24Regular />
            <Text weight="semibold" size={500}>
              React Sample
            </Text>
          </div>
          <Divider />
          <ul className={styles.navList}>
            {navItems.map((item) => (
              <li key={item.to}>
                <NavLink
                  to={item.to}
                  className={({ isActive }) =>
                    `${styles.navItem} ${isActive ? styles.navItemActive : ""}`
                  }
                >
                  {item.icon}
                  {item.label}
                </NavLink>
              </li>
            ))}
          </ul>
          <Divider />
          <div className={styles.sidebarFooter}>
            {isAuthenticated ? (
              <Button
                appearance="subtle"
                icon={<SignOut24Regular />}
                onClick={handleLogout}
                style={{ width: "100%" }}
              >
                Sign out
              </Button>
            ) : (
              <Button
                appearance="primary"
                onClick={handleLogin}
                style={{ width: "100%" }}
              >
                Sign in
              </Button>
            )}
          </div>
        </nav>
        <div className={styles.main}>
          <header className={styles.header}>
            <Text size={400} weight="semibold">
              Diginsight React Sample
            </Text>
            <div className={styles.headerRight}>
              {isAuthenticated && <Text size={300}>{userName}</Text>}
              <AboutDialog />
            </div>
          </header>
          <main className={styles.content}>
            <Outlet />
          </main>
        </div>
      </div>
    </FluentProvider>
  );
}
