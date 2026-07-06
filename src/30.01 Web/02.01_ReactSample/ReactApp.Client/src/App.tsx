import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import {
  MsalProvider,
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
} from "@azure/msal-react";
import type { IPublicClientApplication } from "@azure/msal-browser";
import {
  FluentProvider,
  Button,
  Text,
  MessageBar,
  MessageBarBody,
  makeStyles,
} from "@fluentui/react-components";
import { Bot48Regular } from "@fluentui/react-icons";
import { getMsalInstance } from "./auth/msalInstance";
import { getLoginRequest } from "./auth/authConfig";
import { basePath } from "./api/clientConfigApi";
import { appTheme } from "./theme/appTheme";
import Layout from "./components/Layout";
import AgentPage from "./pages/AgentPage";

const useStyles = makeStyles({
  loginContainer: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    justifyContent: "center",
    height: "100vh",
    backgroundColor: "#f6f7f8",
  },
  loginCard: {
    backgroundColor: "#ffffff",
    borderRadius: "8px",
    minWidth: "380px",
    boxShadow: "0 8px 24px rgba(0,0,0,0.08)",
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    padding: "40px 32px 32px 32px",
    gap: "12px",
  },
  brand: {
    color: "#0F6CBD",
  },
  title: {
    fontSize: "24px",
    fontWeight: 600,
  },
  subtitle: {
    fontSize: "14px",
    color: "#616161",
    marginBottom: "12px",
    textAlign: "center",
  },
  signInButton: {
    width: "100%",
    height: "44px",
  },
  footer: {
    fontSize: "12px",
    color: "#9e9e9e",
    marginTop: "16px",
  },
});

function LoginScreen({ isAuthConfigured }: { isAuthConfigured: boolean }) {
  const styles = useStyles();
  const instance = getMsalInstance();

  return (
    <FluentProvider theme={appTheme}>
      <div className={styles.loginContainer}>
        <div className={styles.loginCard}>
          <Bot48Regular className={styles.brand} />
          <Text className={styles.title}>Diginsight React Sample</Text>
          <Text className={styles.subtitle}>
            Sign in with your Microsoft account to talk to the Weather Agent.
          </Text>
          {isAuthConfigured ? (
            <Button
              appearance="primary"
              size="large"
              className={styles.signInButton}
              onClick={() => instance.loginRedirect(getLoginRequest())}
            >
              Sign in
            </Button>
          ) : (
            <MessageBar intent="warning">
              <MessageBarBody>
                Authentication is not configured. Start ReactApp.Api with the
                "https - Testmc" profile so it can advertise the app registration.
              </MessageBarBody>
            </MessageBar>
          )}
          <Text className={styles.footer}>Diginsight samples</Text>
        </div>
      </div>
    </FluentProvider>
  );
}

export default function App({
  instance,
  isAuthConfigured,
}: {
  instance: IPublicClientApplication;
  isAuthConfigured: boolean;
}) {
  return (
    <MsalProvider instance={instance}>
      <UnauthenticatedTemplate>
        <LoginScreen isAuthConfigured={isAuthConfigured} />
      </UnauthenticatedTemplate>
      <AuthenticatedTemplate>
        <BrowserRouter basename={basePath || "/"}>
          <Routes>
            <Route element={<Layout />}>
              <Route path="/agent" element={<AgentPage />} />
              <Route path="*" element={<Navigate to="/agent" replace />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </AuthenticatedTemplate>
    </MsalProvider>
  );
}
