import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  DialogTrigger,
  Table,
  TableBody,
  TableCell,
  TableRow,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { Info24Regular } from "@fluentui/react-icons";
import { useMsal } from "@azure/msal-react";
import { getUserName } from "../auth/authConfig";
import { apiBaseUrl } from "../api/clientConfigApi";

const useStyles = makeStyles({
  key: {
    fontFamily: "monospace",
    color: tokens.colorNeutralForeground3,
    whiteSpace: "nowrap",
    paddingRight: "16px",
  },
  value: {
    fontFamily: "monospace",
    wordBreak: "break-all",
  },
});

export default function AboutDialog() {
  const styles = useStyles();
  const { accounts } = useMsal();
  const account = accounts[0] ?? null;

  const entries: { label: string; value: string | undefined }[] = [
    { label: "Build version", value: import.meta.env.VITE_BUILD_VERSION },
    { label: "API base URL", value: apiBaseUrl },
    { label: "Signed-in user", value: getUserName(account) || "—" },
    { label: "Tenant", value: account?.tenantId ?? "—" },
  ];

  return (
    <Dialog>
      <DialogTrigger disableButtonEnhancement>
        <Button appearance="subtle" icon={<Info24Regular />} aria-label="About" />
      </DialogTrigger>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>About this sample</DialogTitle>
          <DialogContent>
            <Table>
              <TableBody>
                {entries.map((entry) => (
                  <TableRow key={entry.label}>
                    <TableCell className={styles.key}>{entry.label}</TableCell>
                    <TableCell className={styles.value}>{entry.value ?? "—"}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </DialogContent>
          <DialogActions>
            <DialogTrigger disableButtonEnhancement>
              <Button appearance="primary">Close</Button>
            </DialogTrigger>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
